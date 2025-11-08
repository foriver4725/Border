# Border

<img height="256" alt="border_using" src="https://github.com/user-attachments/assets/2949bcba-f51d-4617-95db-7d630b93e470" />

## 概述

该库在 Unity 编辑器和运行时环境中都能使用，  
提供 **高性能的多边形区域检测功能**。  

你可以定义多边形边界区域，判断某个点是否位于其中，  
并在区域内生成或测试随机坐标。  

整个系统以性能为核心设计，  
实现了 **零分配**，  
即使在性能敏感的场景中也能保持流畅运行。  

此外，还支持灵活的图层与颜色自定义，  
以适应不同的使用场景。  

---

## 安装方法

从 [最新发布页面](https://github.com/foriver4725/Border/releases)  
下载资源包并导入到 Unity 项目中。

---

## 主要功能

### 在编辑器中使用

在场景中放置 *Border* 预制体，  
并移动其子对象（称为“Pin”）。  

线段会按照层级视图中的顺序连接。  
计算时会 **忽略 Y 坐标**，因此无需关注高度。  

在 *Border* 组件的检查器中，  
可以设置线条颜色和粗细。  

通过分配不同的图层，你可以将多个 *Border* 进行分组，  
并在后续计算中选择性地排除特定图层。  

<img height="256" alt="border_pins" src="https://github.com/user-attachments/assets/ffebdc95-0560-438c-afea-b267632d07dc" />
<img height="512" alt="border_component" src="https://github.com/user-attachments/assets/f309f510-031f-470f-9cb8-e47b3e9554d4" />

---

### 在脚本中使用

*Border* 组件提供的方法定义在  
[`IBorder`](https://github.com/foriver4725/Border/blob/main/Assets/foriver4725/Border/Assets/Scripts/IBorder.cs) 接口中（完整列表见下方）。  

获取 *Border* 组件的引用后，  
即可直接调用这些方法。  

所有方法均在 **零分配** 条件下运行，  
并尽可能使用内联优化。  

下方展示了执行 **一百万次** 调用时的性能结果：  

<img height="256" alt="border_performance" src="https://github.com/user-attachments/assets/be38d80a-e028-4790-8d8c-7d2b32aba458" />

| 方法 | GC Alloc | Time ms | Self ms |
| --- | --- | --- | --- |
| DoesContain | 0 B | 1080.73 | 1080.73 |
| GetRandomPositionSimply | 0 B | 2286.19 | 2286.19 |
| GetRandomPositionAccurately | 0 B | 2549.61 | 2549.61 |

如前所述，可以为 *Border* 指定图层。  
在带有图层参数的 `DoesContain` 重载中，  
若 *Border* 不属于传入的任何图层候选项，则会自动判定为不包含。  

```cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace foriver4725.Border
{
    internal interface IBorder
    {
        // 若计算成功，返回 true
        bool DoesContain(Vector2 pos, out bool outResult);
        bool DoesContain(Vector2 pos, byte layer, out bool outResult);
        bool DoesContain(Vector2 pos, ReadOnlySpan<byte> layers, out bool outResult); // 基本形式
        bool DoesContain(Vector2 pos, IReadOnlyList<byte> layers, out bool outResult);
        // 若计算失败，返回 false
        bool DoesContain(Vector2 pos);
        bool DoesContain(Vector2 pos, byte layer);
        bool DoesContain(Vector2 pos, ReadOnlySpan<byte> layers);
        bool DoesContain(Vector2 pos, IReadOnlyList<byte> layers);

        // 三维版本
        bool DoesContain(Vector3 pos, out bool outResult);
        bool DoesContain(Vector3 pos, byte layer, out bool outResult);
        bool DoesContain(Vector3 pos, ReadOnlySpan<byte> layers, out bool outResult);
        bool DoesContain(Vector3 pos, IReadOnlyList<byte> layers, out bool outResult);
        bool DoesContain(Vector3 pos);
        bool DoesContain(Vector3 pos, byte layer);
        bool DoesContain(Vector3 pos, ReadOnlySpan<byte> layers);
        bool DoesContain(Vector3 pos, IReadOnlyList<byte> layers);

        // 随机坐标生成
        bool GetRandomPositionSimply(out Vector2 outResult);
        bool GetRandomPositionSimply(float y, out Vector3 outResult);
        Vector2 GetRandomPositionSimply();
        Vector3 GetRandomPositionSimply(float y);

        bool GetRandomPositionAccurately(out Vector2 outResult);
        bool GetRandomPositionAccurately(float y, out Vector3 outResult);
        Vector2 GetRandomPositionAccurately();
        Vector3 GetRandomPositionAccurately(float y);
    }
}
```
