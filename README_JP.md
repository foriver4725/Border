# Border

<img height="256" alt="border_using" src="https://github.com/user-attachments/assets/2949bcba-f51d-4617-95db-7d630b93e470" />

## 概要

このライブラリは、Unityエディタおよびランタイムの両方で動作する、  
**高性能なポリゴン領域判定機能**を提供します。  

ポリゴン形状の領域を定義し、点がその中に含まれているかを判定したり、  
領域内でランダムな座標を生成・検証したりすることができます。  

パフォーマンスを最重視して設計されており、  
**ゼロアロケーション**で実行されるため、  
負荷の高い処理環境でも滑らかに動作します。  

さらに、レイヤーやカラーを柔軟にカスタマイズでき、  
さまざまなユースケースに適応可能です。  

---

## 導入方法

[最新のリリース](https://github.com/foriver4725/Border/releases) ページから  
アセットパッケージをダウンロードし、Unityプロジェクトにインポートしてください。

---

## 主な機能

### エディタ上での使用

シーンに *Border* プレハブを配置し、  
子オブジェクトであるピンを移動させます。  

ピンは**ヒエラルキー上の順番**に沿って線で結ばれます。  
計算時には **Y座標は無視** されるため、平面上の位置だけを気にすればOKです。  

*Border* コンポーネントのインスペクタから、  
線の色や太さを指定できます。  

また、レイヤーを設定することで *Border* をグループ化でき、  
後述の計算処理で特定のレイヤーを除外することも可能です。  

<img height="256" alt="border_pins" src="https://github.com/user-attachments/assets/ffebdc95-0560-438c-afea-b267632d07dc" />
<img height="512" alt="border_component" src="https://github.com/user-attachments/assets/f309f510-031f-470f-9cb8-e47b3e9554d4" />

---

### スクリプトからの使用

*Border* コンポーネントが提供するメソッドは  
[`IBorder`](https://github.com/foriver4725/Border/blob/main/Assets/foriver4725/Border/Assets/Scripts/IBorder.cs)
インターフェースに定義されています（以下に完全一覧を記載）。  

*Border* コンポーネントの参照を取得し、必要なメソッドを呼び出すだけで利用できます。  

これらのメソッドはすべて **ゼロアロケーションで動作** し、  
可能な限りインライン展開されるよう最適化されています。  

以下は、100万回の実行を行った際のプロファイリング結果です。  

<img height="256" alt="border_performance" src="https://github.com/user-attachments/assets/be38d80a-e028-4790-8d8c-7d2b32aba458" />

| メソッド | GC Alloc | Time ms | Self ms |
| --- | --- | --- | --- |
| DoesContain | 0 B | 1080.73 | 1080.73 |
| GetRandomPositionSimply | 0 B | 2286.19 | 2286.19 |
| GetRandomPositionAccurately | 0 B | 2549.61 | 2549.61 |

また、`DoesContain` メソッドにはレイヤー指定付きのオーバーロードがあり、  
指定したレイヤーに属さない *Border* は自動的に除外されます。  

```cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace foriver4725.Border
{
    internal interface IBorder
    {
        // 計算が成功した場合に true を返す
        bool DoesContain(Vector2 pos, out bool outResult);
        bool DoesContain(Vector2 pos, byte layer, out bool outResult);
        bool DoesContain(Vector2 pos, ReadOnlySpan<byte> layers, out bool outResult); // 基本形
        bool DoesContain(Vector2 pos, IReadOnlyList<byte> layers, out bool outResult);
        // 失敗した場合は false を返す
        bool DoesContain(Vector2 pos);
        bool DoesContain(Vector2 pos, byte layer);
        bool DoesContain(Vector2 pos, ReadOnlySpan<byte> layers);
        bool DoesContain(Vector2 pos, IReadOnlyList<byte> layers);

        // Vector3 版
        bool DoesContain(Vector3 pos, out bool outResult);
        bool DoesContain(Vector3 pos, byte layer, out bool outResult);
        bool DoesContain(Vector3 pos, ReadOnlySpan<byte> layers, out bool outResult);
        bool DoesContain(Vector3 pos, IReadOnlyList<byte> layers, out bool outResult);
        bool DoesContain(Vector3 pos);
        bool DoesContain(Vector3 pos, byte layer);
        bool DoesContain(Vector3 pos, ReadOnlySpan<byte> layers);
        bool DoesContain(Vector3 pos, IReadOnlyList<byte> layers);

        // ランダム座標取得
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
