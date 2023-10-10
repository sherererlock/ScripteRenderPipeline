# TBDR

#### 名词解释

**System On Chip**

把CPU、GPU、内存、通信基带、GPS模块等等整合在一起的芯片的称呼

**System Memory**

GPU和CPU共用一块片内LPDDR物理内存

**On-chip Memory**

CPU和GPU的高速SRAM的Cache缓存

**Stall**

当一个GPU核心的两次计算结果之间有依赖关系而必须串行时，等待的过程便是Stall

**FillRate**

像素填充率 = ROP运行的时钟频率 x ROP的个数 x 每个时钟ROP可以处理的像素个数

#### 流程

**TBR** 

VS -- Clip, Project & Cull -- Tiling -- Raster -- Early Visibility Test -- Texture and Shade -- Alpha Test -- Late Visibility Test -- AlphaBlend

**TBDR**

VS -- Clip, Project & Cull -- Tiling -- Raster -- HSR/FPK & DepthTest -- TagBuffer -- Texture and Shade -- Alpha Test -- Late Visibility Test -- AlphaBlend

#### IMR

VS -- Clip, Project & Cull -- Raster -- Early Visibility Test -- Texture and Shade -- Alpha Test -- Late Visibility Test -- AlphaBlend

**IMR的劣势**

- z test跟[blending](https://www.zhihu.com/search?q=blending&search_source=Entity&hybrid_search_source=Entity&hybrid_search_extra={"sourceType"%3A"answer"%2C"sourceId"%3A136096531})都要频繁从framebuffer里读数据，毕竟framebuffer是位于Memory上，带宽压力和功耗自然高；
- Overdraw的问题，比如Application在一帧里先画了棵树，然后画了面墙刚好遮住了树，在IMR下树仍然要在Pixel Shader里Sample texture，而Texture也是放在Memory，[访存功耗](https://www.zhihu.com/search?q=访存功耗&search_source=Entity&hybrid_search_source=Entity&hybrid_search_extra={"sourceType"%3A"answer"%2C"sourceId"%3A136096531})大

#### 差别

TBR一般的实现策略是对于cpu过来的commandbuffer，只对他们做vetex process，然后对vs产生的结果暂时保存，等待非得刷新整个FrameBuffer的时候，才真正的随这批绘制做光栅化，做tile-based-rendering

刷新整个FrameBuffer的时机：（调用所有gpu觉得不得不把这块fb绘制好的时候的操作）

- 调用glBindFrameBuffer改变FrameBuffer时
- 调用glFramebufferTexture*或者glFramebufferRenderbuffer改变attachments
- 调用swapbuffer
- 调用glFlush或者glFinish时
- glreadpixels
- glcopytexiamge
- glbitframebuffer

#### 核心目的

**降低带宽，减少功耗，但渲染帧率上并不比IMR**

#### 优化

- 记得不使用Framebuffer的时候clear或者discard
- 不要在一帧里面频繁的切换framebuffer的绑定
- 对于移动平台，建议你使用 Alpha 混合，而非 Alpha 测试
- 手机上必须要做Alpha Test，先做一遍Depth prepass
- 图片尽量压缩 例如:ASTC  ETC2
- 图片尽量走 mipmap 
- MSAA
- 少在FS 中使用 discard 函数，调用gl_FragDepth从而打断Early-DT( HLSL中为Clip，GLSL中为discard )
- 在移动端的TB(D)R架构中，顶点处理部分，容易成为瓶颈，避免使用曲面细分shader，置换贴图等负操作，提倡使用模型LOD,本质上减少FrameData的压力
- 在每帧渲染之前尽量clear(最小化加载的tile数据量)
- 避免大量的drawcall和顶点量

------

#### ShaderCore(着色核心)

- Execution Engine:执行shader/数学运算
- Load/Store Unit：对memory的存取
- Varying Unit:对VS传入到PS的变量做内插值
- ZS/Blend Unit: z-test和blend，访问tile-memory
- TextureUnit:对纹理内存的操作

#### FowardPixelKilling

Early-Z Test是以Quad为单位测试的，只要其中有一个没被遮掉，就会执行管线的其余流程。FowardPixelKilling的工作原理是，如果发现有fragment会遮挡掉其他的，就会把其他的thread停止掉，粒度更细化

#### RenderPass

-  开始一个Renderpass时需要初始化tile memory
- 结束时可能需要写回到system mem中
- 尽可能减少RenderPasses

#### Vertex Pass

Position Shading - Face Test Culling - Frustum Test Culling - Sample Test Culling - Varying Shading - Polygon List

#### Fragment Pass

Polygon List - Rasterize - Early Z Test - Fragment Thread Creator -- Process - Late Z Test - Blend -- Tile RAM - Tile Write - Transaction Elimination(Compression) - FrameBuffer

#### 优化建议

1. 每次只处理一个RenderPass
   - 不要频繁切换FrameBuffer
2. 最小化RenderPass开始时的Tile加载
   - glClear, glClearBuffer,glInValidateFrameBuffer()
3. 最小化RenderPass结束时的Tile Store
   - glInValidateFrameBuffer()
