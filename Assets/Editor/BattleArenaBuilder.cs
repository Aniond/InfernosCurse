using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

// Builds (or rebuilds) the FFT battle test arena: grid visuals, camera, unit and
// highlight prefabs, cursor, action-menu UI, forecast, and the code-driven
// battle bootstrap. Menu: InfernosCurse → Build Battle Arena.
public static class BattleArenaBuilder
{
    [MenuItem("InfernosCurse/Build Battle Arena")]
    public static void Build()
    {
        var basicAttack = AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/Data/Skills/Skill_BasicAttack.asset");
        var curseClaw   = AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/Data/Skills/Skill_CurseClaw.asset");
        var bakerJob    = AssetDatabase.LoadAssetAtPath<JobDefinition>("Assets/Data/Jobs/Baker/Job_Baker.asset");
        var ergot       = AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/Data/Jobs/Baker/Skills/Skill_ErgotBloom.asset");
        var ashwood     = AssetDatabase.LoadAssetAtPath<SkillDefinition>("Assets/Data/Jobs/Baker/Skills/Skill_AshwoodPeel.asset");
        if (basicAttack == null)
        {
            Debug.LogError("[ArenaBuilder] Skill_BasicAttack.asset not found — aborting.");
            return;
        }

        var knob   = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        var square = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── Camera (battle plane is XY; ortho camera looks +Z) ────────────────
        var camGo = new GameObject("Main Camera");
        var cam = camGo.AddComponent<Camera>();
        camGo.AddComponent<AudioListener>();
        camGo.tag = "MainCamera";
        cam.orthographic = true;
        cam.orthographicSize = 4.5f;
        cam.transform.position = new Vector3(0.5f, 3.25f, -10f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.07f, 0.10f);

        // ── Grid + checkerboard ground ────────────────────────────────────────
        var gridGo = new GameObject("BattleGrid");
        var grid = gridGo.AddComponent<BattleGrid>();
        grid.Initialize(14, 12);

        var visRoot = new GameObject("[GridVisuals]");
        for (int x = 0; x < 14; x++)
            for (int y = 0; y < 12; y++)
            {
                var t = new GameObject($"tile_{x}_{y}");
                t.transform.SetParent(visRoot.transform);
                var sr = t.AddComponent<SpriteRenderer>();
                sr.sprite = square;
                sr.color = ((x + y) % 2 == 0) ? new Color(0.22f, 0.20f, 0.24f) : new Color(0.26f, 0.24f, 0.28f);
                sr.sortingOrder = 0;
                t.transform.position = grid.GridToWorld(new Vector2Int(x, y), 0);
                t.transform.localScale = new Vector3(0.095f, 0.048f, 1f);
            }

        // ── Prefabs ───────────────────────────────────────────────────────────
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Battle"))
            AssetDatabase.CreateFolder("Assets/Prefabs", "Battle");

        var unitGo = new GameObject("BattleUnit_Test");
        var usr = unitGo.AddComponent<SpriteRenderer>();
        usr.sprite = knob;
        usr.sortingOrder = 10;
        unitGo.transform.localScale = Vector3.one * 0.045f;
        unitGo.AddComponent<BattleUnit>();
        unitGo.AddComponent<EnemyAI>();
        var unitPrefab = PrefabUtility.SaveAsPrefabAsset(unitGo, "Assets/Prefabs/Battle/BattleUnit_Test.prefab");
        Object.DestroyImmediate(unitGo);

        GameObject MakeTilePrefab(string name, int sort)
        {
            var g = new GameObject(name);
            var s = g.AddComponent<SpriteRenderer>();
            s.sprite = square; s.sortingOrder = sort;
            g.transform.localScale = new Vector3(0.09f, 0.045f, 1f);
            var p = PrefabUtility.SaveAsPrefabAsset(g, $"Assets/Prefabs/Battle/{name}.prefab");
            Object.DestroyImmediate(g);
            return p;
        }
        var tileMove  = MakeTilePrefab("Highlight_Tile", 5);
        var tileHover = MakeTilePrefab("Highlight_Hover", 6);

        // ── BattleManager ─────────────────────────────────────────────────────
        var bmGo = new GameObject("BattleManager");
        var bm = bmGo.AddComponent<BattleManager>();
        bm.Grid = grid;
        bm.battleUnitPrefab = unitPrefab;
        bm.basicAttackSkill = basicAttack;

        // ── Cursor ────────────────────────────────────────────────────────────
        var cursorVis = new GameObject("CursorVisual");
        var csr = cursorVis.AddComponent<SpriteRenderer>();
        csr.sprite = knob; csr.color = new Color(1f, 0.9f, 0.3f); csr.sortingOrder = 20;
        cursorVis.transform.localScale = Vector3.one * 0.03f;

        var cursorGo = new GameObject("BattleCursor");
        var cursor = cursorGo.AddComponent<BattleCursor>();
        cursor.cursorObject     = cursorVis;
        cursor.moveTilePrefab   = tileMove;
        cursor.attackTilePrefab = tileMove;
        cursor.hoverTilePrefab  = tileHover;

        // ── Action menu UI ────────────────────────────────────────────────────
        var canvasGo = new GameObject("BattleCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        var menuGo = new GameObject("ActionMenu");
        menuGo.transform.SetParent(canvasGo.transform, false);
        var menu = menuGo.AddComponent<ActionMenu>();

        var panel = new GameObject("Panel");
        panel.transform.SetParent(menuGo.transform, false);
        panel.AddComponent<Image>().color = new Color(0.10f, 0.08f, 0.06f, 0.92f);
        var prt = panel.GetComponent<RectTransform>();
        prt.anchorMin = new Vector2(1f, 0.5f); prt.anchorMax = new Vector2(1f, 0.5f);
        prt.pivot = new Vector2(1f, 0.5f);
        prt.anchoredPosition = new Vector2(-16f, 0f);
        prt.sizeDelta = new Vector2(240f, 330f);
        menu.panel = panel;

        (GameObject go, Image bg, TMP_Text label) MakeButton(string name, float yOff)
        {
            var b = new GameObject(name);
            b.transform.SetParent(panel.transform, false);
            var img = b.AddComponent<Image>();
            img.color = SkillButton.NormalColor;
            var brt = b.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.5f, 1f); brt.anchorMax = new Vector2(0.5f, 1f);
            brt.pivot = new Vector2(0.5f, 1f);
            brt.anchoredPosition = new Vector2(0f, yOff);
            brt.sizeDelta = new Vector2(216f, 46f);
            var lgo = new GameObject("Label");
            lgo.transform.SetParent(b.transform, false);
            var l = lgo.AddComponent<TextMeshProUGUI>();
            l.fontSize = 20; l.alignment = TextAlignmentOptions.Left; l.color = Color.white;
            var lrt = l.rectTransform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(10f, 2f); lrt.offsetMax = new Vector2(-10f, -2f);
            return (b, img, l);
        }

        menu.skillButtons = new SkillButton[4];
        for (int i = 0; i < 4; i++)
        {
            var (bgo, bimg, blabel) = MakeButton($"Skill_{i}", -12f - i * 54f);
            bgo.AddComponent<Button>();
            var sbtn = bgo.AddComponent<SkillButton>();
            sbtn.skillNameLabel = blabel;
            sbtn.background = bimg;
            var spGo = new GameObject("SP");
            spGo.transform.SetParent(bgo.transform, false);
            var sp = spGo.AddComponent<TextMeshProUGUI>();
            sp.fontSize = 15; sp.alignment = TextAlignmentOptions.Right; sp.color = new Color(0.7f, 0.85f, 1f);
            var sprt = sp.rectTransform;
            sprt.anchorMin = Vector2.zero; sprt.anchorMax = Vector2.one;
            sprt.offsetMin = new Vector2(10f, 2f); sprt.offsetMax = new Vector2(-10f, -2f);
            sbtn.spCostLabel = sp;
            menu.skillButtons[i] = sbtn;
        }

        var wait = MakeButton("WaitBtn", -12f - 4 * 54f);
        wait.label.text = "Wait";
        menu.waitButton = wait.go.AddComponent<Button>();
        var back = MakeButton("BackBtn", -12f - 5 * 54f);
        back.label.text = "Back";
        menu.backButton = back.go.AddComponent<Button>();

        var det = new GameObject("DetailPanel");
        det.transform.SetParent(canvasGo.transform, false);
        det.AddComponent<Image>().color = new Color(0.10f, 0.08f, 0.06f, 0.92f);
        var drt = det.GetComponent<RectTransform>();
        drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.zero;
        drt.pivot = Vector2.zero;
        drt.anchoredPosition = new Vector2(16f, 16f);
        drt.sizeDelta = new Vector2(360f, 160f);
        menu.detailPanel = det;

        TMP_Text MakeDetail(string name, float y)
        {
            var g = new GameObject(name);
            g.transform.SetParent(det.transform, false);
            var t = g.AddComponent<TextMeshProUGUI>();
            t.fontSize = 16; t.alignment = TextAlignmentOptions.TopLeft; t.color = Color.white;
            var rt = t.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, y);
            rt.sizeDelta = new Vector2(-20f, 24f);
            return t;
        }
        menu.detailSkillName   = MakeDetail("Name", -8f);
        menu.detailDescription = MakeDetail("Desc", -34f);
        menu.detailPower       = MakeDetail("Power", -84f);
        menu.detailRange       = MakeDetail("Range", -104f);
        menu.detailSPCost      = MakeDetail("SP", -124f);
        menu.detailDamageType  = MakeDetail("Type", -144f);

        // ── Forecast, input, bootstrap ────────────────────────────────────────
        new GameObject("BattleForecast").AddComponent<BattleForecastUI>();

        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<EventSystem>();
        esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

        var startGo = new GameObject("BattleTestStarter");
        var starter = startGo.AddComponent<BattleTestStarter>();
        starter.testJob = bakerJob;
        starter.preUnlockedJobSkills = new[] { ashwood, ergot };
        starter.enemySkills = new[] { curseClaw };

        bool saved = EditorSceneManager.SaveScene(scene, "Assets/Scenes/BattleArena.unity");
        Debug.Log($"[ArenaBuilder] BattleArena built and saved={saved}. Press Play to start a battle. " +
                  "Controls: WASD/arrows = cursor, Z/Enter = confirm, X/Esc = cancel.");
    }
}
