using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Splines;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

/// <summary>
/// 这个类用来把spline绘制出多边形映射到一张贴图上，分为立即(初始化)和动画(解锁)两种模式
/// 可以通过MenuContext菜单 来执行 process 和 unlock 操作 注意要
/// 最好直接挂在fog的GameObject上
/// </summary>
[ExecuteInEditMode]
public class PolygonMask : MonoBehaviour
{
    public enum MaskMode
    {
        UnlockMode = 1,
        InitMode = 2,
    }

    public enum EaseType
    {
        line,
        EaseInOut,
        EaseOut,
        EaseIn

    }

    public ComputeShader maskCS;
    public Texture2D sourceTex;   //

    public SplineContainer spline;

    public MaskMode mode = MaskMode.InitMode;
    public EaseType easeType = EaseType.line;
    public float gradientWidth = 10.0f;
    public float expand = 3.0f;    // 膨胀（像素）
    public RenderTexture rt;//旧结果（持久）
    ComputeBuffer polygonBuffer;
    public bool isProgress = false;
    public float progress = 0.0f;      // 0~1 动画进度
    public float _StepExpand = 5.0f;    // 羽化（像素）

    public Vector4 edge = new Vector4(-200f, 200f,-200f, 200f);

    private bool isReady = false;

    void Update()
    {
        if (mode == MaskMode.InitMode) return;
        if (!isReady) return;
        if (isProgress && progress < 1)
        {
            progress += Time.deltaTime * 0.3f;
            bool isLastStep = progress >= 1;
            progress = Mathf.Clamp01(progress);
            float easedProgress = 0.0f;
            if(easeType == EaseType.line){
                easedProgress = progress;
            }
            else if(easeType == EaseType.EaseInOut){
                easedProgress = EaseInOut(progress);
            }
            else if(easeType == EaseType.EaseOut){
                easedProgress = EaseOut(progress);
            }
            else if(easeType == EaseType.EaseIn){
                easedProgress = EaseIn(progress);
            }
            
            ProcessCompute(easedProgress);
            MergeComputer();
            
#if UNITY_EDITOR
            if(isLastStep){
                SaveRTToPNG(rt, savePath);
            }
#else
            if(isLastStep){
                _tempRT.Release();// 释放临时RT
                _tempRT = null;
            }
#endif
        }
    }

    float EaseInOut(float t)
    {
        return t * t * (3f - 2f * t);
    }
    float EaseOut(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }
    float EaseIn(float t)
    {
        return t * t * t;
    }

    private int textureSize = 0;
    void ProcessCompute(float easedProgress)
    {
        int kernel = maskCS.FindKernel("CSMain");
        maskCS.SetFloat("_Progress", easedProgress);
        maskCS.SetFloat("_TimeValue", Time.deltaTime);
        maskCS.Dispatch(
            kernel,
            Mathf.CeilToInt(textureSize / 8.0f),
            Mathf.CeilToInt(textureSize / 8.0f),
            1
        );
    }

    void MergeComputer()
    {
        int kernel = mergeCS.FindKernel("CSMain");
        mergeCS.SetTexture(kernel, "Result", rt);
        mergeCS.SetTexture(kernel, "NewTex", _tempRT);
        mergeCS.SetInt("_Width", textureSize);
        mergeCS.SetInt("_Height", textureSize);
        mergeCS.Dispatch(kernel, textureSize / 8, textureSize / 8, 1);

    }

    public int groupIdx = 0;
    public Vector2Int UVTile = new Vector2Int(0,0);
    void MaskComputer(int groupIdx,RenderTexture rt)
    {
        List<Vector2> polygon = new List<Vector2>();
        float minX = edge.x;//-200f;
        float maxX = edge.y;//200f;
        float minZ = edge.z;//-200f;
        float maxZ = edge.w;//200f;

        foreach (var knot in spline[groupIdx].Knots)
        {
            Vector3 world = spline.transform.TransformPoint(knot.Position);
            
            Vector2 uv = new Vector2(
                (world.x - minX) / (maxX - minX)- UVTile.x,
                (world.z - minZ) / (maxZ - minZ)- UVTile.y
            );
            Debug.Log("world:"+world+"uv:"+uv);
            polygon.Add(uv);
        }


        // 闭合（保险）
        if (polygon[0] != polygon[polygon.Count - 1])
            polygon.Add(polygon[0]);
        

        polygonBuffer = new ComputeBuffer(polygon.Count, sizeof(float) * 2);
        polygonBuffer.SetData(polygon);

        int kernel = maskCS.FindKernel("CSMain");

        maskCS.SetInt("_Width", textureSize);
        maskCS.SetInt("_Height", textureSize);

        maskCS.SetInt("_PointCount", polygon.Count);
        maskCS.SetBuffer(kernel, "_Polygon", polygonBuffer);

        maskCS.SetTexture(kernel, "Result", rt);
        maskCS.SetFloat("_Expand", expand);    // 膨胀（像素）

        maskCS.SetInt("_Mode", (int)mode);              // 渐变模式
        maskCS.SetFloat("_GradientWidth", gradientWidth); // 控制渐变范围

        maskCS.SetFloat("_Progress", progress);
        maskCS.SetFloat("_StepExpand", _StepExpand);    // 羽化（像素）

        maskCS.Dispatch(
            kernel,
            Mathf.CeilToInt(textureSize / 8.0f),
            Mathf.CeilToInt(textureSize / 8.0f),
            1
        );
    }

    [ContextMenu("Init")]
    public void InitMask()
    {
        if (maskCS == null) return;
        if (sourceTex == null) return;
        isProgress = false;
        mode = MaskMode.InitMode;
        progress = 1.0f;
        int textureSize = sourceTex.width;

        // ===== 1️⃣ 创建 RenderTexture =====
        rt = new RenderTexture(textureSize, textureSize, 0);
        rt.enableRandomWrite = true;
        rt.Create();
        Graphics.Blit(sourceTex, rt);
        this.textureSize = textureSize;
        MaskComputer(groupIdx,rt);
        isReady = true;

        // ===== 5️⃣ 显示结果 =====
        // GetComponent<Renderer>().material.SetTexture("_Mask", rt);
        GetComponent<Fog>().materialInst.SetTexture("_Mask", rt);

#if UNITY_EDITOR
        if(Application.isPlaying == false){
            SaveRTToPNG(rt, savePath);
        }
#endif
    }

    void Start()
    {
        if(rt != null){
            rt.Release();
            rt = null;
        }
        if(_tempRT != null){
            _tempRT.Release();
            _tempRT = null;
        }   
        // GetComponent<Renderer>().material.SetTexture("_Mask", null);
        // GetComponent<Fog>().materialInst.SetTexture("_Mask", null);
    }
#if UNITY_EDITOR
    public string savePath = "Assets/ThirdPlugins/VolmetricFog/PolygonMask/PolygonMask.png";

    public static void SaveRTToPNG(RenderTexture rt, string path)
    {
        RenderTexture currentRT = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RFloat, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();

        RenderTexture.active = currentRT;

        // ⭐ 转成可保存格式（RFloat不能直接PNG）
        Texture2D finalTex = ConvertToRGBA(tex);

        byte[] bytes = finalTex.EncodeToPNG();
        File.WriteAllBytes(path, bytes);
        AssetDatabase.Refresh();

        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(finalTex);

        Debug.Log("Saved PNG to: " + path);
    }
    public static Texture2D ConvertToRGBA(Texture2D src)
    {
        int w = src.width;
        int h = src.height;

        Texture2D dst = new Texture2D(w, h, TextureFormat.RGBA32, false);

        Color[] pixels = src.GetPixels();
        Color[] newPixels = new Color[pixels.Length];

        for (int i = 0; i < pixels.Length; i++)
        {
            float v = pixels[i].r; // 取R通道
            newPixels[i] = new Color(v, v, v, 1);
        }

        dst.SetPixels(newPixels);
        dst.Apply();

        return dst;
    }
#endif

    /// /////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public int unlockGroupIdx = 1;
    public ComputeShader mergeCS;
    [ContextMenu("Unlock")]
    public void Unlock()
    {
        if(isReady == false)return;

        mode = MaskMode.UnlockMode;
        progress = 0;
        isProgress = true;
        _tempRT = new RenderTexture(textureSize, textureSize, 0);
        _tempRT.enableRandomWrite = true;
        _tempRT.Create();
        
        MaskComputer(unlockGroupIdx,_tempRT);
    }
    public RenderTexture _tempRT;//新区域临时计算




}