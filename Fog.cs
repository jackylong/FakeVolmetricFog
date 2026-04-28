using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// 伪体积雾效果实现类
/// 使用ComputeShader计算大量四边形的位置和旋转矩阵,
/// 通过GPU实例化绘制实现高性能的体积雾效果
/// </summary>
public class Fog : MonoBehaviour {
    // GPU线程组大小常量
    const int BLOCK_SIZE = 16;

    // 需要实例化的网格(通常是四边形面片)
    public Mesh mesh;
    // 渲染材质,包含雾效果的shader
    public Material material;
    // 计算着色器,用于在GPU上计算位置和旋转
    public ComputeShader computeShader;    

    // 渲染边界,用于剔除和视锥体剔除
    private Bounds bounds;
    // 存储每个四边形位置的GPU缓冲区
    private ComputeBuffer positionBuffer;
    // 存储每个四边形旋转矩阵的GPU缓冲区
    private ComputeBuffer rotationMatrixBuffer;
    // 间接绘制参数缓冲区,用于GPU实例化绘制
    private ComputeBuffer argsBuffer;
    // 间接绘制参数数组
    private uint[] args = new uint[5];
    [Header("设置四边形的数量,决定雾的密度")]
    public uint numeberOfQuads = 60000;

    // 位置数组,用于初始化数据
    private Vector3[] positions;
    [Header("设置雾区域的最大边界坐标(用于随机分布四边形)")]
    public Vector3 maxPos = new Vector3(200f, 1f, 200f);
    public Vector2 wind = new Vector2(5f, 0f);

    public ComputeShader computeShaderInst;
    public Material materialInst;


    /// <summary>
    /// 初始化函数
    /// 创建GPU缓冲区,初始化四边形位置和旋转矩阵,
    /// 设置材质缓冲区和间接绘制参数
    /// </summary>
    void Start()
    {
        Camera.main.depthTextureMode |= DepthTextureMode.Depth;

        computeShaderInst = Instantiate(computeShader);
        materialInst = Instantiate(material);
        // 创建位置缓冲区,每个Vector3占用12字节(3个float,每个4字节)
        positionBuffer = new ComputeBuffer((int)numeberOfQuads, 12);
        // 创建旋转矩阵缓冲区,每个4x4矩阵占用16个float
        rotationMatrixBuffer = new ComputeBuffer((int)numeberOfQuads, Marshal.SizeOf(typeof(Matrix4x4)));
        // 初始化CPU侧的位置数组
        positions = new Vector3[numeberOfQuads];

        // 初始化旋转矩阵数组
        Matrix4x4[] mat = new Matrix4x4[numeberOfQuads];
        for (int i = 0; i < numeberOfQuads; i++)
        {
            // 在指定区域内随机生成四边形位置
            positions[i] = new Vector3(Random.Range(-maxPos.x, maxPos.x)+this.transform.position.x, Random.Range(-maxPos.y, maxPos.y)+this.transform.position.y, Random.Range(-maxPos.z, maxPos.z)+this.transform.position.z );
            // 初始化为单位矩阵(无旋转)
            mat[i] = Matrix4x4.identity;
        }

        // 将CPU数据上传到GPU缓冲区
        positionBuffer.SetData(positions);
        rotationMatrixBuffer.SetData(mat);
        // 将缓冲区传递给材质,在shader中使用
        materialInst.SetBuffer("PositionBuffer", positionBuffer);
        materialInst.SetBuffer("RotationMatrixBuffer", rotationMatrixBuffer);
        materialInst.SetVector("_FogCenter", new Vector4(this.transform.position.x,this.transform.position.y, this.transform.position.z,0));

        // 查找计算着色器的主kernel函数
        int kernelId = computeShaderInst.FindKernel("CSMain");
        // 设置缓冲区到计算着色器
        computeShaderInst.SetBuffer(kernelId, "PositionBuffer", positionBuffer);
        computeShaderInst.SetBuffer(kernelId, "RotationMatrixBuffer", rotationMatrixBuffer);
        computeShaderInst.SetVector("_Bounds", new Vector2(maxPos.x, maxPos.z));
        computeShaderInst.SetVector("_FogCenter", new Vector2(this.transform.position.x, this.transform.position.z));

        // 创建渲染边界,用于视锥体剔除
        bounds = new Bounds(Vector3.zero, new Vector3(numeberOfQuads / 3, numeberOfQuads / 3, numeberOfQuads / 3));

        // 创建间接绘制参数缓冲区
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        args[0] = mesh.GetIndexCount(0);      // 每个实例的索引数量
        args[1] = numeberOfQuads;             // 实例数量
        args[2] = mesh.GetIndexStart(0);      // 起始索引
        args[3] = mesh.GetBaseVertex(0);      // 基顶点索引
        args[4] = 0;                          // 起始实例索引
        argsBuffer.SetData(args);
    }

    /*  // 备注
     *  计算着色器调度示例:
     *  shader.dispatch(Kernel, 4096, 1, 1)
     *  
     *  如果在计算着色器中定义:
     *  [numthreads(1024,1,1)]
     *  
     *  则总共可以处理 4096*1024 = 4,194,304 个粒子
     */
    
    /// <summary>
    /// 每帧更新函数
    /// 调度ComputeShader计算新的位置和旋转矩阵,
    /// 并执行GPU实例化绘制
    /// </summary>
    void Update()
    {
        // 查找计算着色器的主kernel函数
        int kernelId = computeShaderInst.FindKernel("CSMain");
        // 设置缓冲区到计算着色器

        // 计算需要的线程组数量(每个线程组处理BLOCK_SIZE个元素)
        int groupSize = Mathf.CeilToInt(numeberOfQuads / BLOCK_SIZE);
        // 调度计算着色器,在GPU上并行计算所有四边形的位置和旋转
        
        // 传递时间参数,用于动画效果
        computeShaderInst.SetFloat("_Time", Time.time);
        computeShaderInst.SetFloat("_Delta", Time.deltaTime);
        computeShaderInst.SetVector("_Wind", wind);
        // computeShaderInst.SetVector("_Bounds", new Vector2(maxPos.x, maxPos.z));
        // computeShaderInst.SetVector("_FogCenter", new Vector2(this.transform.position.x, this.transform.position.z));
        computeShaderInst.Dispatch(kernelId, groupSize, 1, 1);
        bounds.center = this.transform.position;
        
        // // 使用间接绘制进行GPU实例化渲染,这是性能关键点
        Graphics.DrawMeshInstancedIndirect(mesh, 0, materialInst, bounds, argsBuffer);
    }

    /// <summary>
    /// 销毁函数
    /// 释放所有GPU缓冲区资源,防止内存泄漏
    /// </summary>
    void OnDestroy()
    {
        positionBuffer.Release();
        rotationMatrixBuffer.Release();
        argsBuffer.Release();
    }
}
