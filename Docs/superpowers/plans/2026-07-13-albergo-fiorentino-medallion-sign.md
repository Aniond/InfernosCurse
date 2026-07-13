# Albergo Fiorentino Medallion Sign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a Blender-authored, double-sided Florentine medallion sign, integrate it into the Mercato inn façade, and verify its readability and camera behavior in Unity.

**Architecture:** A deterministic Blender Python script creates the complete static prop and exports one GLB containing named submeshes for the board, rim, lily, lettering, bracket, and hangers. `MercatoVecchioProductionKitBuilder` instances that GLB into the façade prefab and validates its authored structure before rebuilding the live Mercato scene.

**Tech Stack:** Blender 5.1 Python API, glTF 2.0/GLB, Unity 6.4, C# editor tooling, URP, prefab/scene validators.

## Global Constraints

- The sign is circular, gently beveled, double-sided, approximately 1.35 metres in diameter and 0.14 metres thick.
- Both faces read `ALBERGO FIORENTINO` using the project's Cinzel TTF converted to mesh.
- The finish uses dark chestnut wood, worn brass, warm ivory lettering, a simplified Florentine lily, and forged black iron.
- The existing façade mounting area and `InnFacade_` renderer naming convention remain intact.
- The sign is static and must not alter inn gameplay, camera profiles, or the standalone inn interior.
- Existing unrelated working-tree changes must be preserved.

---

### Task 1: Add a failing authored-sign validator

**Files:**
- Modify: `Assets/Editor/MercatoVecchioProductionKitBuilder.cs:378-402`

**Interfaces:**
- Consumes: `Mercato_InnFacade.prefab`.
- Produces: `static void ValidateInnSign(List<string> errors)` called by `Validate()`.

- [ ] **Step 1: Write the structural regression check**

Call `ValidateInnSign(errors);` after `ValidateFountain(errors);`. Load the prefab contents, reject the legacy slab, and require these authored transforms:

```csharp
static readonly string[] RequiredInnSignParts =
{
    "InnFacade_SignMedallion", "InnFacade_SignRim",
    "InnFacade_SignBracket", "InnFacade_SignHanger_Left", "InnFacade_SignHanger_Right",
    "InnFacade_SignLily_North", "InnFacade_SignLily_South",
    "InnFacade_SignText_North", "InnFacade_SignText_South",
};

static void ValidateInnSign(List<string> errors)
{
    GameObject root = PrefabUtility.LoadPrefabContents($"{PrefabRoot}/Mercato_InnFacade.prefab");
    try
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        foreach (string name in RequiredInnSignParts)
        {
            Transform part = Array.Find(transforms, transform => transform.name == name);
            if (part == null) errors.Add("inn medallion sign is missing " + name);
            else if (part.GetComponentsInChildren<Renderer>(true).Length == 0)
                errors.Add(name + " has no renderer");
        }
        if (Array.Exists(transforms, transform => transform.name == "InnFacade_Sign"))
            errors.Add("legacy rectangular inn sign remains");
    }
    finally { PrefabUtility.UnloadPrefabContents(root); }
}
```

- [ ] **Step 2: Run the validator to prove the red state**

Run `InfernosCurse > Validation > Validate Mercato Production Kit`.

Expected: failure listing the missing medallion pieces and the legacy rectangular sign.

---

### Task 2: Author and preview the Blender prop

**Files:**
- Create: `Tools/Blender/build_albergo_fiorentino_sign.py`
- Create through Blender: `ArtSource/Blender/AlbergoFiorentinoSign.blend`
- Create through Blender: `Assets/Environment/MercatoVecchio/ProductionKit/Models/AlbergoFiorentinoSign.glb`
- Create through Blender: `Temp/AlbergoFiorentinoSign_Front.png`
- Create through Blender: `Temp/AlbergoFiorentinoSign_Back.png`

**Interfaces:**
- Consumes: `Assets/UI/Fonts/Cinzel.ttf`.
- Produces: a Unity-scale GLB with the exact transform names required by Task 1.

- [ ] **Step 1: Build the reproducible Blender script**

The script clears the scene, creates four Principled BSDF materials, then builds:

```python
add_cylinder("InnFacade_SignRim", radius=0.72, depth=0.12, material=brass)
add_cylinder("InnFacade_SignMedallion", radius=0.65, depth=0.15, material=chestnut)
add_box("InnFacade_SignBracket", location=(0.0, 0.775, 0.88), scale=(0.12, 1.55, 0.12), material=iron)
add_torus("InnFacade_SignHanger_Left", location=(0.0, -0.27, 0.75), material=iron)
add_torus("InnFacade_SignHanger_Right", location=(0.0, 0.27, 0.75), material=iron)
```

All disk geometry lies in the Blender Y/Z plane so glTF's Unity import keeps the sign perpendicular to the façade and readable from the north/south street approaches. Apply a bevel modifier to the board, rim, and bracket before export.

- [ ] **Step 2: Create raised double-sided identity meshes**

Load Cinzel and create `ALBERGO\nFIORENTINO` as centered text on each face. Use 0.008 metre extrusion with a restrained bevel, convert each face to mesh, and name the objects `InnFacade_SignText_North` and `InnFacade_SignText_South`. Create each lily from joined, flattened petal/stem meshes and name them `InnFacade_SignLily_North` and `InnFacade_SignLily_South`.

- [ ] **Step 3: Export, save the source, and render previews**

Run:

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.1\blender.exe' --background --python 'Tools\Blender\build_albergo_fiorentino_sign.py'
```

Expected: exit code 0, one GLB under the production-kit model folder, one `.blend` source, and front/back PNG previews. The script prints the exported mesh count and triangle count; target is below 20,000 triangles while retaining beveled raised lettering.

- [ ] **Step 4: Inspect both rendered previews**

Confirm the complete name is upright and readable on both faces, the circular silhouette is unbroken, the lily does not collide with the text, and the bracket/hangers meet the top of the medallion.

---

### Task 3: Integrate the GLB into the deterministic façade builder

**Files:**
- Modify: `Assets/Editor/MercatoVecchioProductionKitBuilder.cs:8-18,91-105`
- Modify through rebuild: `Assets/Environment/MercatoVecchio/ProductionKit/Prefabs/Mercato_InnFacade.prefab`

**Interfaces:**
- Consumes: `AlbergoFiorentinoSign.glb` at `InnSignModelPath`.
- Produces: `InstanceModel(...)` and an `InnFacade_SignAssembly` instance at façade-local `(2.1, 4.35, -1.55)`.

- [ ] **Step 1: Add the model path and instance helper**

```csharp
const string InnSignModelPath = Root + "/Models/AlbergoFiorentinoSign.glb";

static GameObject InstanceModel(Transform parent, string name, string path, Vector3 position)
{
    GameObject source = Require<GameObject>(path);
    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
    instance.name = name;
    instance.transform.SetParent(parent, false);
    instance.transform.localPosition = position;
    return instance;
}
```

- [ ] **Step 2: Replace the primitive sign and bracket**

Remove the two `Box` calls for `InnFacade_SignBracket` and `InnFacade_Sign`. Add:

```csharp
InstanceModel(root.transform, "InnFacade_SignAssembly", InnSignModelPath,
    new Vector3(2.1f, 4.35f, -1.55f));
```

- [ ] **Step 3: Rebuild the kit and prove the validator turns green**

Run `InfernosCurse > Mercato Vecchio > 1. Rebuild Production Kit`.

Expected:

```text
[MercatoProductionKitValidator] Validation passed for 7 reusable production prefabs.
```

---

### Task 4: Rebuild and playtest Mercato

**Files:**
- Modify through rebuild: `Assets/Scenes/MercatoVecchio.unity`

**Interfaces:**
- Consumes: rebuilt façade prefab and existing production-scene builder.
- Produces: the live double-sided sign under `AlbergoFiorentino_Facade` with existing cutaway coverage.

- [ ] **Step 1: Rebuild the production scene**

Run `InfernosCurse > Mercato Vecchio > 2. Rebuild Production Scene`.

Expected: production validation passes and there are no C# compilation errors.

- [ ] **Step 2: Inspect both street approaches in Game view**

Enter Play mode and inspect the sign while approaching from north and south. Confirm both faces are upright and readable, the sign is mounted rather than floating, and no mesh flicker occurs.

- [ ] **Step 3: Run the seamless-inn verifier**

Run `InfernosCurse > Validation > Run Mercato Seamless Play Mode Probe`.

Expected: five exterior-to-inn-to-exterior crossings pass with façade cutaway and exterior restoration intact.

---

### Task 5: Record and integrity-check the completion

**Files:**
- Modify: `Docs/MASTER_COMPLETION_REGISTER.md`

**Interfaces:**
- Consumes: Blender preview, production validator, live Game-view, and seamless-probe evidence.
- Produces: `MC-2026-07-13-003 - Albergo Fiorentino medallion sign` with status `Verified current`.

- [ ] **Step 1: Add the verified completion entry**

Record the Blender source/output paths, double-sided readability, deterministic builder integration, both street-view checks, and verifier result.

- [ ] **Step 2: Run final checks**

```powershell
git diff --check -- Assets/Editor/MercatoVecchioProductionKitBuilder.cs Docs/MASTER_COMPLETION_REGISTER.md
rg -n "InnSignModelPath|SignMedallion|SignText_North|SignText_South|MC-2026-07-13-003" Assets/Editor/MercatoVecchioProductionKitBuilder.cs Tools/Blender/build_albergo_fiorentino_sign.py Docs/MASTER_COMPLETION_REGISTER.md
```

Expected: no whitespace errors, required identifiers present, and no recent `error CS` or `Compilation failed` entries in Unity's Editor log.
