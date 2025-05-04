# Kokoro TTS Unity

Work in Progress: A port of the [Kokoro](https://github.com/hexgrad/kokoro/) TTS to Unity.

https://github.com/user-attachments/assets/a4d7aefb-d606-40cb-bf7b-73db95798857

## How to run

### Fix Errors

Currently, the project shows the following error in Unity 6 (6000.0.46f1). Open the `AnimationClipUpgrader.cs` and manually edit the file to fix the errors.

![errors](https://github.com/user-attachments/assets/a0468e02-1f25-409e-80f0-eb465a9fb838)

```log
Library/PackageCache/com.unity.render-pipelines.universal@18be219df6cb/Editor/AnimationClipUpgrader.cs(186,33): error CS0576: Namespace '<global namespace>' contains a definition conflicting with alias 'UID'
```

```diff
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using IMaterial = UnityEditor.Rendering.UpgradeUtility.IMaterial;
-using UID = UnityEditor.Rendering.UpgradeUtility.UID;

namespace UnityEditor.Rendering
{
+    using UID = UnityEditor.Rendering.UpgradeUtility.UID;

    /// <summary>
    /// A class containing static methods for updating <see cref="AnimationClip"/> assets with bindings for <see cref="Material"/> properties.
    /// </summary>
```

### Run on macOS Editor

Clone the repository and run the `Assets/Scenes/KokoroTTSDemo.unity` scene in Unity Editor.

### Run on iOS and maybe other AOT platforms

Kokoro TTS Unity depends on the Catalyst - C# NLP (Natural Language Processing) library. To use it on an AOT required platform such as iOS, you need to clone my [fork of the Catalyst](https://github.com/asus4/catalyst) and replace the .dll in the `Assets/Packages` folder. See [`build-catalyst.sh`](build-catalyst.sh) for details.
