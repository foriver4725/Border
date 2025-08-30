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
