# Border

<img height="256" alt="border_using" src="https://github.com/user-attachments/assets/2949bcba-f51d-4617-95db-7d630b93e470" />


## Description
This library provides high-performance polygon region checks that work both at runtime and in the Unity Editor.<br/>
It enables you to define polygonal regions, determine whether a point is contained inside, and generate or test random coordinates.<br/>
Designed with performance in mind, the system runs with zero allocations, ensuring smooth and efficient execution even in performance-critical scenarios.<br/>
In addition, it supports flexible customization of layers and colors to fit different use cases.<br/>

## How to Use
Download the asset package from [the latest release](https://github.com/foriver4725/Border/releases) and import it into your project.<br/>

## Features

### Editor Usage
Place the *Border* prefab in your scene and move the pin child objects.<br/>
Lines will be drawn in the order of the pins in the hierarchy.<br/>
Note: The Y coordinate is ignored during calculation, so you don’t need to worry about it.<br/>
From the Border component, you can specify the line color and thickness.<br/>
By assigning layers, you can group Borders together, and in the calculation process introduced later, you can exclude specific layers if needed.<br/>

<img height="256" alt="border_pins" src="https://github.com/user-attachments/assets/ffebdc95-0560-438c-afea-b267632d07dc" />
<img height="512" alt="border_component" src="https://github.com/user-attachments/assets/f309f510-031f-470f-9cb8-e47b3e9554d4" />

### Usage from Scripts
The methods provided by the Border component can be found in the `IBorder` interface (a full list is also provided below).<br/>
Obtain a reference to the Border component and call the required methods as needed.<br/>
All of these methods are designed to run with **zero allocations** and are aggressively inlined wherever possible.<br/>
For reference, profiling results for one million executions are shown below.<br/>

<img height="256" alt="border_performance" src="https://github.com/user-attachments/assets/be38d80a-e028-4790-8d8c-7d2b32aba458" />

```cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace foriver4725.Border
{
    internal interface IBorder
    {
        // Returns if the calculation was successful
        bool DoContains(Vector2 pos, out bool outResult);
        bool DoContains(Vector2 pos, byte layer, out bool outResult);
        bool DoContains(Vector2 pos, ReadOnlySpan<byte> layers, out bool outResult); // Base
        bool DoContains(Vector2 pos, IReadOnlyList<byte> layers, out bool outResult);
        // If the calculation failed, returns false
        bool DoContains(Vector2 pos);
        bool DoContains(Vector2 pos, byte layer);
        bool DoContains(Vector2 pos, ReadOnlySpan<byte> layers);
        bool DoContains(Vector2 pos, IReadOnlyList<byte> layers);

        // Returns if the calculation was successful
        bool DoContains(Vector3 pos, out bool outResult);
        bool DoContains(Vector3 pos, byte layer, out bool outResult);
        bool DoContains(Vector3 pos, ReadOnlySpan<byte> layers, out bool outResult); // Base
        bool DoContains(Vector3 pos, IReadOnlyList<byte> layers, out bool outResult);
        // If the calculation failed, returns false
        bool DoContains(Vector3 pos);
        bool DoContains(Vector3 pos, byte layer);
        bool DoContains(Vector3 pos, ReadOnlySpan<byte> layers);
        bool DoContains(Vector3 pos, IReadOnlyList<byte> layers);

        // Returns if the calculation was successful
        bool GetRandomPositionSimply(out Vector2 outResult);
        bool GetRandomPositionSimply(float y, out Vector3 outResult); // Base
        // If the calculation failed, returns Vector2.zero or Vector3.zero
        Vector2 GetRandomPositionSimply();
        Vector3 GetRandomPositionSimply(float y);

        // Returns if the calculation was successful
        bool GetRandomPositionAccurately(out Vector2 outResult);
        bool GetRandomPositionAccurately(float y, out Vector3 outResult); // Base
        // If the calculation failed, returns Vector2.zero or Vector3.zero
        Vector2 GetRandomPositionAccurately();
        Vector3 GetRandomPositionAccurately(float y);
    }
}
```
