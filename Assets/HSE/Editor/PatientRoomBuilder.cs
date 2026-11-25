using System.Collections.Generic;
using System.IO;
using HSE.Environment;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HSE.EditorTools
{
    /// <summary>
    /// Builds the inpatient room used by the nurse training scenario from primitives,
    /// generated textures and URP Lit materials, then drops in the VR template's XR rig.
    /// Menu: HSE > Build Patient Room Scene. Safe to re-run: it rebuilds the scene file.
    /// </summary>
    public static class PatientRoomBuilder
    {
        const string Root = "Assets/HSE";
        const string MatDir = Root + "/Materials";
        const string TexDir = Root + "/Textures";
        const string SceneDir = Root + "/Scenes";
        const string ScenePath = SceneDir + "/PatientRoom.unity";
        const string XrRigPath = "Assets/VRTemplateAssets/Prefabs/Setup/Complete XR Origin Set Up Variant.prefab";

        // Room interior: x -3..3, z -2.5..2.5, height 2.9. North (+z) is the bed's headwall.
        const float RoomW = 6f, RoomD = 5f, RoomH = 2.9f, WallT = 0.1f;
        const float BedX = 0.3f, BedZ = 1.35f;

        static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();

        [MenuItem("HSE/Build Patient Room Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(Root); EnsureFolder(MatDir); EnsureFolder(TexDir); EnsureFolder(SceneDir);
            Mats.Clear();
            CreateMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var env = Group("Environment", null, Vector3.zero);
            BuildShell(Group("Room", env.transform, Vector3.zero).transform);
            BuildWindow(Group("Window", env.transform, Vector3.zero).transform);
            BuildExterior(Group("Exterior", env.transform, Vector3.zero).transform);
            BuildCeiling(Group("Ceiling", env.transform, Vector3.zero).transform);

            var furniture = Group("Furniture", env.transform, Vector3.zero).transform;
            BuildBed(furniture, out var bedRoot, out var headPivot);
            BuildNurseStool(Group("NurseStool", furniture, new Vector3(-0.8f, 0, 1.0f), 90).transform);
            BuildArmchair(Group("VisitorArmchair", furniture, new Vector3(2.35f, 0, -0.3f), -90).transform);
            BuildBedsideCabinet(Group("BedsideCabinet", furniture, new Vector3(1.3f, 0, 2.15f)).transform);
            BuildOverbedTable(Group("OverbedTable", furniture, new Vector3(0.95f, 0, 0.7f)).transform);
            BuildWardrobe(Group("Wardrobe", furniture, new Vector3(2.4f, 0, -2.2f)).transform);
            BuildCurtain(Group("PrivacyCurtain", furniture, Vector3.zero).transform);
            BuildHeadwall(Group("Headwall", furniture, new Vector3(BedX, 0, RoomD / 2), 0).transform);
            BuildSinkStation(Group("HandHygieneStation", furniture, new Vector3(-RoomW / 2, 0, -0.9f), -90).transform);
            BuildWhiteboard(Group("CareBoard", furniture, new Vector3(0.6f, 0, -RoomD / 2), 180).transform);
            BuildClock(Group("WallClock", furniture, new Vector3(-RoomW / 2, 0, 0f), -90).transform);

            var equipment = Group("Equipment", null, Vector3.zero).transform;
            BuildVitalsMonitor(Group("VitalsMonitorMount", equipment, new Vector3(-0.95f, 0, RoomD / 2), 0).transform);
            BuildBpCuffHolder(Group("BPCuffHolder", equipment, new Vector3(-1.4f, 0, RoomD / 2), 0).transform);
            BuildIvPole(Group("IVPole", equipment, new Vector3(-0.6f, 0, 2.15f)).transform);
            BuildCommsTerminal(Group("CommsTerminal", equipment, new Vector3(-RoomW / 2, 0, 0.9f), -90).transform);
            BuildProcedureTrolley(Group("ProcedureTrolley", equipment, new Vector3(-1.45f, 0, 0.05f), 90).transform);

            BuildPatient(bedRoot.transform, headPivot.transform);
            BuildLighting(Group("Lighting", null, Vector3.zero).transform);
            BuildPlayer();

            MarkStatic(env);
            PdaBuilder.SetupInScene();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[HSE] Patient room built and saved to " + ScenePath);
        }

        // ------------------------------------------------------------------ room shell

        static void BuildShell(Transform p)
        {
            float hw = RoomW / 2, hd = RoomD / 2;
            var floor = Box("Floor", p, new Vector3(0, -0.05f, 0), new Vector3(RoomW + 0.2f, 0.1f, RoomD + 0.2f), M("Floor"));
            AddTeleportArea(floor);

            Box("Wall_North", p, new Vector3(0, RoomH / 2, hd + WallT / 2), new Vector3(RoomW + 0.2f, RoomH, WallT), M("Wall"));
            Box("Wall_West", p, new Vector3(-hw - WallT / 2, RoomH / 2, 0), new Vector3(WallT, RoomH, RoomD + 0.2f), M("Wall"));

            // South wall with a 1.2 m door opening (x -2.7..-1.5, h 2.2).
            float zS = -hd - WallT / 2;
            Box("Wall_South_A", p, new Vector3(-2.9f, RoomH / 2, zS), new Vector3(0.4f, RoomH, WallT), M("Wall"));
            Box("Wall_South_B", p, new Vector3(0.8f, RoomH / 2, zS), new Vector3(4.6f, RoomH, WallT), M("Wall"));
            Box("Wall_South_Lintel", p, new Vector3(-2.1f, 2.55f, zS), new Vector3(1.2f, 0.7f, WallT), M("Wall"));

            // East wall with a window opening (z -1.2..1.2, y 0.9..2.3).
            float xE = hw + WallT / 2;
            Box("Wall_East_Below", p, new Vector3(xE, 0.45f, 0), new Vector3(WallT, 0.9f, RoomD + 0.2f), M("Wall"));
            Box("Wall_East_Above", p, new Vector3(xE, 2.6f, 0), new Vector3(WallT, 0.6f, RoomD + 0.2f), M("Wall"));
            Box("Wall_East_S", p, new Vector3(xE, 1.6f, -1.9f), new Vector3(WallT, 1.4f, 1.4f + 0.2f), M("Wall"));
            Box("Wall_East_N", p, new Vector3(xE, 1.6f, 1.9f), new Vector3(WallT, 1.4f, 1.4f + 0.2f), M("Wall"));

            // Wainscot, bumper rail and skirting on every wall.
            var trim = Group("WallTrim", p, Vector3.zero).transform;
            void Band(string n, Vector3 c, Vector3 along, float len)
            {
                bool alongX = Mathf.Abs(along.x) > 0.5f;
                Vector3 S(float h, float d) => alongX ? new Vector3(len, h, d) : new Vector3(d, h, len);
                Vector3 inward = -new Vector3(Mathf.Sign(c.x) * (alongX ? 0 : 1), 0, Mathf.Sign(c.z) * (alongX ? 1 : 0));
                NoCol(Box(n + "_Wainscot", trim, c + inward * 0.005f + Vector3.up * 0.45f, S(0.9f, 0.01f), M("Wainscot")));
                NoCol(Box(n + "_Rail", trim, c + inward * 0.02f + Vector3.up * 0.85f, S(0.08f, 0.04f), M("WoodLight")));
                NoCol(Box(n + "_Skirting", trim, c + inward * 0.01f + Vector3.up * 0.05f, S(0.1f, 0.02f), M("Skirting")));
            }
            Band("North", new Vector3(0, 0, hd), Vector3.right, RoomW);
            Band("West", new Vector3(-hw, 0, 0), Vector3.forward, RoomD);
            Band("East", new Vector3(hw, 0, 0), Vector3.forward, RoomD);
            Band("SouthA", new Vector3(-2.865f, 0, -hd), Vector3.right, 0.27f);
            Band("SouthB", new Vector3(0.765f, 0, -hd), Vector3.right, 4.47f);

            // Door: frame, leaf with vision panel, handles.
            var door = Group("Door", p, Vector3.zero).transform;
            Box("Frame_L", door, new Vector3(-2.73f, 1.1f, -hd - 0.02f), new Vector3(0.06f, 2.2f, 0.14f), M("PlasticWhite"));
            Box("Frame_R", door, new Vector3(-1.47f, 1.1f, -hd - 0.02f), new Vector3(0.06f, 2.2f, 0.14f), M("PlasticWhite"));
            Box("Frame_Top", door, new Vector3(-2.1f, 2.23f, -hd - 0.02f), new Vector3(1.32f, 0.06f, 0.14f), M("PlasticWhite"));
            Box("Leaf", door, new Vector3(-2.1f, 1.08f, -hd - 0.03f), new Vector3(1.14f, 2.14f, 0.05f), M("Door"));
            NoCol(Box("VisionPanel", door, new Vector3(-1.8f, 1.45f, -hd - 0.03f), new Vector3(0.18f, 0.6f, 0.06f), M("DarkGlass")));
            NoCol(Box("KickPlate", door, new Vector3(-2.1f, 0.15f, -hd - 0.003f), new Vector3(1.1f, 0.25f, 0.005f), M("Metal")));
            NoCol(Box("Handle", door, new Vector3(-1.66f, 1.0f, -hd + 0.03f), new Vector3(0.16f, 0.025f, 0.03f), M("Metal")));
            NoCol(Box("HandleRose", door, new Vector3(-1.6f, 1.0f, -hd + 0.0f), new Vector3(0.05f, 0.05f, 0.02f), M("Metal")));
        }

        static void BuildWindow(Transform p)
        {
            float x = RoomW / 2;
            Box("Sill", p, new Vector3(x - 0.08f, 0.9f, 0), new Vector3(0.3f, 0.04f, 2.5f), M("WoodLight"));
            Box("Frame_Top", p, new Vector3(x + 0.02f, 2.27f, 0), new Vector3(0.1f, 0.06f, 2.4f), M("PlasticWhite"));
            Box("Frame_Bottom", p, new Vector3(x + 0.02f, 0.93f, 0), new Vector3(0.1f, 0.06f, 2.4f), M("PlasticWhite"));
            Box("Frame_S", p, new Vector3(x + 0.02f, 1.6f, -1.17f), new Vector3(0.1f, 1.4f, 0.06f), M("PlasticWhite"));
            Box("Frame_N", p, new Vector3(x + 0.02f, 1.6f, 1.17f), new Vector3(0.1f, 1.4f, 0.06f), M("PlasticWhite"));
            Box("Mullion", p, new Vector3(x + 0.02f, 1.6f, 0), new Vector3(0.08f, 1.4f, 0.05f), M("PlasticWhite"));
            Box("Glass", p, new Vector3(x + 0.03f, 1.6f, 0), new Vector3(0.01f, 1.4f, 2.36f), M("Glass"));

            // Venetian blind, partly lowered.
            var blind = Group("Blind", p, Vector3.zero).transform;
            NoCol(Box("Headrail", blind, new Vector3(x - 0.06f, 2.25f, 0), new Vector3(0.06f, 0.05f, 2.36f), M("PlasticWhite")));
            for (int i = 0; i < 12; i++)
                NoCol(Box("Slat_" + i, blind, new Vector3(x - 0.06f, 2.2f - i * 0.028f, 0), new Vector3(0.045f, 0.004f, 2.32f), M("PlasticWhite"), new Vector3(0, 0, -25)));
            NoCol(Box("BottomRail", blind, new Vector3(x - 0.06f, 2.2f - 12 * 0.028f, 0), new Vector3(0.05f, 0.015f, 2.34f), M("PlasticWhite")));

            // Pot plant on the sill.
            var plant = Group("Plant", p, new Vector3(x - 0.1f, 0.92f, 0.75f)).transform;
            Cyl("Pot", plant, new Vector3(0, 0.07f, 0), 0.14f, 0.14f, M("Terracotta"));
            NoCol(Sph("Leaves_A", plant, new Vector3(0, 0.22f, 0), new Vector3(0.22f, 0.2f, 0.22f), M("Leaves")));
            NoCol(Sph("Leaves_B", plant, new Vector3(0.05f, 0.3f, 0.06f), new Vector3(0.14f, 0.14f, 0.14f), M("Leaves")));
            NoCol(Sph("Leaves_C", plant, new Vector3(-0.05f, 0.29f, -0.05f), new Vector3(0.13f, 0.15f, 0.13f), M("Leaves")));
        }

        static void BuildExterior(Transform p)
        {
            NoCol(Box("Sky", p, new Vector3(40, 5, 0), new Vector3(0.2f, 60, 120), M("Sky")));
            NoCol(Box("Ground", p, new Vector3(20, -6, 0), new Vector3(40, 0.2f, 120), M("Grass")));
            var rnd = new System.Random(204);
            for (int i = 0; i < 9; i++)
            {
                float z = -24 + i * 6 + (float)rnd.NextDouble() * 2;
                float h = 10 + (float)rnd.NextDouble() * 14;
                float x = 18 + (float)rnd.NextDouble() * 8;
                var b = Group("Building_" + i, p, new Vector3(x, -6, z)).transform;
                NoCol(Box("Body", b, new Vector3(0, h / 2, 0), new Vector3(6, h, 5), M(i % 2 == 0 ? "Building" : "BuildingWarm")));
                for (float y = 2; y < h - 1; y += 2.2f)
                    NoCol(Box("Windows", b, new Vector3(-3.01f, y, 0), new Vector3(0.02f, 0.9f, 4.2f), M("BuildingGlass")));
            }
            for (int i = 0; i < 6; i++)
            {
                var t = Group("Tree_" + i, p, new Vector3(9 + (float)rnd.NextDouble() * 3, -6, -10 + i * 4f)).transform;
                NoCol(Cyl("Trunk", t, new Vector3(0, 2.5f, 0), 0.4f, 5, M("Bark")));
                NoCol(Sph("Crown", t, new Vector3(0, 5.8f, 0), Vector3.one * (3.2f + (float)rnd.NextDouble()), M("Leaves")));
            }
        }

        static void BuildCeiling(Transform p)
        {
            Box("Ceiling", p, new Vector3(0, RoomH + 0.05f, 0), new Vector3(RoomW + 0.2f, 0.1f, RoomD + 0.2f), M("Ceiling"));
            NoCol(Box("LightPanel_Bed", p, new Vector3(BedX, RoomH - 0.012f, 1.0f), new Vector3(1.2f, 0.025f, 0.6f), M("LightPanel")));
            NoCol(Box("LightPanel_Entry", p, new Vector3(-1.5f, RoomH - 0.012f, -1.2f), new Vector3(1.2f, 0.025f, 0.6f), M("LightPanel")));
            NoCol(Box("LightPanel_Window", p, new Vector3(1.8f, RoomH - 0.012f, -1.0f), new Vector3(0.6f, 0.025f, 0.6f), M("LightPanel")));
            NoCol(Cyl("SmokeDetector", p, new Vector3(-0.5f, RoomH - 0.015f, -0.3f), 0.12f, 0.03f, M("PlasticWhite")));
            NoCol(Box("AirVent", p, new Vector3(-2.2f, RoomH - 0.01f, 1.6f), new Vector3(0.5f, 0.02f, 0.5f), M("PlasticGray")));
            for (int i = 0; i < 6; i++)
                NoCol(Box("AirVent_Slot", p, new Vector3(-2.2f, RoomH - 0.022f, 1.4f + i * 0.08f), new Vector3(0.42f, 0.005f, 0.02f), M("DarkMetal")));
        }

        // ------------------------------------------------------------------ furniture

        static void BuildBed(Transform parent, out GameObject bed, out GameObject headPivot)
        {
            bed = Group("HospitalBed", parent, new Vector3(BedX, 0, BedZ));
            var t = bed.transform;
            Box("UnderFrame", t, new Vector3(0, 0.12f, 0), new Vector3(0.7f, 0.08f, 1.6f), M("DarkMetal"));
            Box("Column_Head", t, new Vector3(0, 0.25f, 0.55f), new Vector3(0.2f, 0.25f, 0.2f), M("PlasticWhite"));
            Box("Column_Foot", t, new Vector3(0, 0.25f, -0.55f), new Vector3(0.2f, 0.25f, 0.2f), M("PlasticWhite"));
            Box("Frame", t, new Vector3(0, 0.37f, 0), new Vector3(0.94f, 0.1f, 2.02f), M("PlasticGray"));
            foreach (var sx in new[] { -1, 1 })
            foreach (var sz in new[] { -1, 1 })
            {
                NoCol(Box("CasterFork", t, new Vector3(sx * 0.32f, 0.11f, sz * 0.78f), new Vector3(0.04f, 0.08f, 0.06f), M("DarkMetal")));
                NoCol(Cyl("Caster", t, new Vector3(sx * 0.32f, 0.055f, sz * 0.78f), 0.1f, 0.035f, M("Black"), new Vector3(0, 0, 90)));
            }

            Box("Mattress_Foot", t, new Vector3(0, 0.5f, -0.35f), new Vector3(0.9f, 0.16f, 1.3f), M("Mattress"));
            NoCol(Box("Sheet_Foot", t, new Vector3(0, 0.585f, -0.35f), new Vector3(0.92f, 0.02f, 1.32f), M("Sheet")));

            headPivot = Group("HeadSection", t, new Vector3(0, 0.5f, 0.3f));
            headPivot.transform.localRotation = Quaternion.Euler(-20, 0, 0);
            var hp = headPivot.transform;
            Box("Mattress_Head", hp, new Vector3(0, 0, 0.375f), new Vector3(0.9f, 0.16f, 0.75f), M("Mattress"));
            NoCol(Box("Sheet_Head", hp, new Vector3(0, 0.085f, 0.375f), new Vector3(0.92f, 0.02f, 0.77f), M("Sheet")));
            Box("Pillow", hp, new Vector3(0, 0.14f, 0.58f), new Vector3(0.62f, 0.11f, 0.32f), M("Sheet"));

            Box("Headboard", t, new Vector3(0, 0.75f, 1.07f), new Vector3(1.0f, 0.6f, 0.05f), M("WoodLight"));
            NoCol(Box("Headboard_Cap", t, new Vector3(0, 1.06f, 1.07f), new Vector3(1.02f, 0.04f, 0.07f), M("PlasticWhite")));
            Box("Footboard", t, new Vector3(0, 0.62f, -1.07f), new Vector3(1.0f, 0.45f, 0.05f), M("WoodLight"));
            NoCol(Box("Footboard_Cap", t, new Vector3(0, 0.86f, -1.07f), new Vector3(1.02f, 0.04f, 0.07f), M("PlasticWhite")));
            NoCol(Box("Footboard_Panel", t, new Vector3(0, 0.72f, -1.1f), new Vector3(0.26f, 0.1f, 0.015f), M("Screen")));

            Box("SideRail_Left_Raised", t, new Vector3(0.49f, 0.8f, 0.55f), new Vector3(0.03f, 0.22f, 0.75f), M("PlasticWhite"));
            Box("SideRail_Right_Lowered", t, new Vector3(-0.49f, 0.45f, 0.55f), new Vector3(0.03f, 0.22f, 0.75f), M("PlasticWhite"));
            Box("SideRail_Left_Foot", t, new Vector3(0.49f, 0.72f, -0.6f), new Vector3(0.03f, 0.18f, 0.6f), M("PlasticWhite"));

            Box("Blanket", t, new Vector3(0, 0.64f, -0.6f), new Vector3(0.94f, 0.12f, 0.9f), M("Blanket"));
            NoCol(Box("Blanket_Fold", t, new Vector3(0, 0.705f, -0.15f), new Vector3(0.95f, 0.03f, 0.18f), M("Sheet")));
            NoCol(Box("Blanket_Drape_L", t, new Vector3(0.475f, 0.52f, -0.6f), new Vector3(0.02f, 0.3f, 0.9f), M("Blanket")));
            NoCol(Box("Blanket_Drape_R", t, new Vector3(-0.475f, 0.52f, -0.6f), new Vector3(0.02f, 0.3f, 0.9f), M("Blanket")));
            foreach (var sx in new[] { -1, 1 })
            {
                NoCol(Cap("Leg_Bump", t, new Vector3(sx * 0.11f, 0.68f, -0.55f), 0.16f, 0.8f, M("Blanket"), new Vector3(90, 0, 0)));
                NoCol(Sph("Foot_Bump", t, new Vector3(sx * 0.12f, 0.72f, -0.92f), new Vector3(0.13f, 0.14f, 0.18f), M("Blanket")));
            }
        }

        static void BuildNurseStool(Transform t)
        {
            Star(t, 0.06f, 5, 0.28f, M("DarkMetal"));
            Cyl("GasLift", t, new Vector3(0, 0.28f, 0), 0.05f, 0.38f, M("Metal"));
            Box("Mechanism", t, new Vector3(0, 0.47f, 0), new Vector3(0.16f, 0.04f, 0.16f), M("DarkMetal"));
            Cyl("Seat", t, new Vector3(0, 0.53f, 0), 0.46f, 0.08f, M("UpholsteryBlue"));
            NoCol(Box("BackPost", t, new Vector3(0, 0.66f, -0.21f), new Vector3(0.04f, 0.26f, 0.03f), M("DarkMetal")));
            Box("BackPad", t, new Vector3(0, 0.84f, -0.23f), new Vector3(0.36f, 0.16f, 0.05f), M("UpholsteryBlue"), new Vector3(-8, 0, 0));
            NoCol(Box("HeightLever", t, new Vector3(0.12f, 0.46f, 0.05f), new Vector3(0.1f, 0.012f, 0.02f), M("Black")));
        }

        static void BuildArmchair(Transform t)
        {
            foreach (var sx in new[] { -1, 1 })
            foreach (var sz in new[] { -1, 1 })
                Cyl("Leg", t, new Vector3(sx * 0.28f, 0.12f, sz * 0.24f), 0.04f, 0.24f, M("WoodLight"));
            Box("SeatBase", t, new Vector3(0, 0.3f, 0), new Vector3(0.66f, 0.14f, 0.6f), M("UpholsteryTeal"));
            Box("Cushion", t, new Vector3(0, 0.41f, 0.02f), new Vector3(0.54f, 0.09f, 0.54f), M("UpholsteryTealLight"));
            Box("Back", t, new Vector3(0, 0.68f, -0.26f), new Vector3(0.66f, 0.62f, 0.12f), M("UpholsteryTeal"), new Vector3(-10, 0, 0));
            foreach (var sx in new[] { -1, 1 })
            {
                Box("Arm", t, new Vector3(sx * 0.33f, 0.47f, 0), new Vector3(0.08f, 0.22f, 0.6f), M("UpholsteryTeal"));
                NoCol(Box("ArmCap", t, new Vector3(sx * 0.33f, 0.59f, 0.02f), new Vector3(0.1f, 0.03f, 0.62f), M("WoodLight")));
            }
        }

        static void BuildBedsideCabinet(Transform t)
        {
            Box("Body", t, new Vector3(0, 0.37f, 0), new Vector3(0.45f, 0.74f, 0.45f), M("WoodLight"));
            NoCol(Box("Top", t, new Vector3(0, 0.75f, 0), new Vector3(0.47f, 0.025f, 0.47f), M("PlasticWhite")));
            NoCol(Box("Drawer", t, new Vector3(-0.228f, 0.6f, 0), new Vector3(0.01f, 0.18f, 0.4f), M("PlasticWhite")));
            NoCol(Box("Door", t, new Vector3(-0.228f, 0.27f, 0), new Vector3(0.01f, 0.4f, 0.4f), M("PlasticWhite")));
            NoCol(Box("DrawerHandle", t, new Vector3(-0.24f, 0.6f, 0), new Vector3(0.015f, 0.02f, 0.16f), M("Metal")));
            NoCol(Box("DoorHandle", t, new Vector3(-0.24f, 0.4f, 0.14f), new Vector3(0.015f, 0.12f, 0.02f), M("Metal")));
            Cyl("Cup", t, new Vector3(-0.08f, 0.815f, -0.1f), 0.07f, 0.1f, M("PlasticWhite"));
            Cyl("WaterJug", t, new Vector3(0.1f, 0.865f, 0.1f), 0.1f, 0.2f, M("Glass"));
            Box("TissueBox", t, new Vector3(0.05f, 0.8f, -0.12f), new Vector3(0.13f, 0.08f, 0.22f), M("UpholsteryTealLight"));
        }

        static void BuildOverbedTable(Transform t)
        {
            Box("Base", t, new Vector3(0, 0.05f, 0), new Vector3(0.12f, 0.05f, 0.62f), M("DarkMetal"));
            foreach (var sz in new[] { -1, 1 })
                NoCol(Cyl("Caster", t, new Vector3(0, 0.03f, sz * 0.27f), 0.06f, 0.03f, M("Black"), new Vector3(0, 0, 90)));
            Box("Column", t, new Vector3(0, 0.48f, 0), new Vector3(0.06f, 0.86f, 0.06f), M("PlasticGray"));
            Box("Top", t, new Vector3(-0.36f, 0.92f, 0), new Vector3(0.86f, 0.03f, 0.42f), M("WoodLight"));
            NoCol(Box("Book", t, new Vector3(-0.5f, 0.95f, 0.06f), new Vector3(0.16f, 0.03f, 0.23f), M("Red")));
            NoCol(Cyl("Cup", t, new Vector3(-0.25f, 0.985f, -0.08f), 0.07f, 0.1f, M("PlasticWhite")));
        }

        static void BuildWardrobe(Transform t)
        {
            Box("Body", t, new Vector3(0, 1.0f, 0), new Vector3(1.0f, 2.0f, 0.5f), M("WoodLight"));
            NoCol(Box("Split", t, new Vector3(0, 1.0f, 0.251f), new Vector3(0.01f, 1.95f, 0.005f), M("Skirting")));
            NoCol(Box("Handle_L", t, new Vector3(-0.06f, 1.05f, 0.27f), new Vector3(0.02f, 0.25f, 0.03f), M("Metal")));
            NoCol(Box("Handle_R", t, new Vector3(0.06f, 1.05f, 0.27f), new Vector3(0.02f, 0.25f, 0.03f), M("Metal")));
        }

        static void BuildCurtain(Transform t)
        {
            float y = RoomH - 0.05f;
            NoCol(Box("Track_Front", t, new Vector3(0.325f, y, 0.15f), new Vector3(2.45f, 0.03f, 0.03f), M("Metal")));
            NoCol(Box("Track_West", t, new Vector3(-0.9f, y, 1.325f), new Vector3(0.03f, 0.03f, 2.35f), M("Metal")));
            NoCol(Box("Track_East", t, new Vector3(1.55f, y, 1.325f), new Vector3(0.03f, 0.03f, 2.35f), M("Metal")));
            for (int i = 0; i < 8; i++)
            {
                float x = 1.12f + i * 0.055f;
                Box("Pleat_" + i, t, new Vector3(x, 1.65f, 0.15f + (i % 2 == 0 ? 0.03f : -0.03f)), new Vector3(0.06f, 2.3f, 0.03f), M("Curtain"), new Vector3(0, i % 2 == 0 ? 35 : -35, 0));
                NoCol(Box("Hook_" + i, t, new Vector3(x, y - 0.05f, 0.15f), new Vector3(0.01f, 0.06f, 0.01f), M("Metal")));
            }
            NoCol(Box("MeshTop", t, new Vector3(1.32f, 2.72f, 0.15f), new Vector3(0.46f, 0.18f, 0.03f), M("CurtainMesh")));
        }

        static void BuildHeadwall(Transform t)
        {
            // Local -Z faces into the room.
            Box("Panel", t, new Vector3(0, 1.4f, -0.06f), new Vector3(2.4f, 0.36f, 0.12f), M("PlasticWhite"));
            NoCol(Box("UplightStrip", t, new Vector3(0, 1.595f, -0.07f), new Vector3(2.4f, 0.03f, 0.1f), M("LightPanel")));
            NoCol(Box("Accent", t, new Vector3(0, 1.3f, -0.121f), new Vector3(2.38f, 0.04f, 0.003f), M("Wainscot")));
            for (int i = 0; i < 3; i++)
            {
                NoCol(Box("Socket_" + i, t, new Vector3(-1.05f + i * 0.14f, 1.42f, -0.122f), new Vector3(0.09f, 0.09f, 0.006f), M("PlasticWhite")));
                NoCol(Box("SocketFace_" + i, t, new Vector3(-1.05f + i * 0.14f, 1.42f, -0.126f), new Vector3(0.05f, 0.05f, 0.004f), M("PlasticGray")));
            }
            NoCol(Cyl("Gas_O2", t, new Vector3(0.75f, 1.42f, -0.135f), 0.06f, 0.03f, M("GreenO2"), new Vector3(90, 0, 0)));
            NoCol(Cyl("Gas_Air", t, new Vector3(0.9f, 1.42f, -0.135f), 0.06f, 0.03f, M("Black"), new Vector3(90, 0, 0)));
            NoCol(Cyl("Gas_Vac", t, new Vector3(1.05f, 1.42f, -0.135f), 0.06f, 0.03f, M("Yellow"), new Vector3(90, 0, 0)));
            NoCol(Cyl("NurseCall", t, new Vector3(-0.6f, 1.42f, -0.13f), 0.05f, 0.02f, M("Red"), new Vector3(90, 0, 0)));
            // Reading lamp above the bed.
            NoCol(Box("ReadingLamp", t, new Vector3(0, 2.05f, -0.1f), new Vector3(0.6f, 0.06f, 0.18f), M("PlasticWhite")));
            NoCol(Box("ReadingLamp_Diffuser", t, new Vector3(0, 2.018f, -0.1f), new Vector3(0.56f, 0.005f, 0.14f), M("LightPanel")));
        }

        static void BuildSinkStation(Transform t)
        {
            Box("Cabinet", t, new Vector3(0, 0.41f, -0.26f), new Vector3(0.72f, 0.82f, 0.5f), M("PlasticWhite"));
            NoCol(Box("CabinetDoorLine", t, new Vector3(0, 0.4f, -0.512f), new Vector3(0.005f, 0.7f, 0.005f), M("PlasticGray")));
            Box("Counter", t, new Vector3(0, 0.84f, -0.27f), new Vector3(0.76f, 0.04f, 0.54f), M("Counter"));
            NoCol(Box("Basin", t, new Vector3(0, 0.862f, -0.29f), new Vector3(0.42f, 0.006f, 0.32f), M("Metal")));
            NoCol(Box("Backsplash", t, new Vector3(0, 1.02f, -0.012f), new Vector3(0.76f, 0.32f, 0.02f), M("Tile")));
            NoCol(Cyl("Faucet", t, new Vector3(0, 0.97f, -0.07f), 0.03f, 0.24f, M("Metal")));
            NoCol(Box("Spout", t, new Vector3(0, 1.08f, -0.14f), new Vector3(0.025f, 0.025f, 0.16f), M("Metal")));
            NoCol(Box("ElbowLever", t, new Vector3(0.06f, 1.02f, -0.08f), new Vector3(0.12f, 0.015f, 0.015f), M("Metal")));

            var rub = Group("HandRubDispenser", t, new Vector3(0.52f, 1.25f, -0.045f)).transform;
            Box("Body", rub, Vector3.zero, new Vector3(0.11f, 0.22f, 0.09f), M("PlasticWhite"));
            NoCol(Box("Front", rub, new Vector3(0, 0.02f, -0.046f), new Vector3(0.09f, 0.12f, 0.004f), M("Accent")));
            NoCol(Box("Nozzle", rub, new Vector3(0, -0.12f, -0.02f), new Vector3(0.03f, 0.02f, 0.03f), M("PlasticGray")));
            Label("Label", rub, new Vector3(0, 0.02f, -0.05f), new Vector2(0.08f, 0.04f), "HAND\nRUB", Color.white, TextAlignmentOptions.Center);

            var gloves = Group("GloveDispenser", t, new Vector3(-0.55f, 1.3f, -0.06f)).transform;
            NoCol(Box("Rack", gloves, new Vector3(0, 0, 0.04f), new Vector3(0.3f, 0.46f, 0.02f), M("Metal")));
            string[] sizes = { "S", "M", "L" };
            string[] mats = { "GloveS", "GloveM", "GloveL" };
            for (int i = 0; i < 3; i++)
            {
                var b = Box("GloveBox_" + sizes[i], gloves, new Vector3(0, 0.15f - i * 0.15f, 0), new Vector3(0.25f, 0.13f, 0.1f), M(mats[i]));
                Label("Size", b.transform.parent, new Vector3(-0.08f, 0.15f - i * 0.15f, -0.052f), new Vector2(0.05f, 0.05f), sizes[i], Color.white, TextAlignmentOptions.Center);
                NoCol(Box("Slot", gloves, new Vector3(0.03f, 0.15f - i * 0.15f, -0.051f), new Vector3(0.1f, 0.04f, 0.004f), M("Sheet")));
            }

            NoCol(Box("PaperTowelDispenser", t, new Vector3(0, 1.52f, -0.065f), new Vector3(0.3f, 0.34f, 0.13f), M("PlasticWhite")));
            NoCol(Box("PaperTowelSlot", t, new Vector3(0, 1.37f, -0.13f), new Vector3(0.2f, 0.01f, 0.01f), M("Black")));

            Label("HygieneSign", t, new Vector3(0, 1.85f, -0.005f), new Vector2(0.6f, 0.12f), "<b>HAND HYGIENE</b>\n<size=60%>before and after every patient contact</size>", new Color(0.1f, 0.35f, 0.45f), TextAlignmentOptions.Center);

            var clinical = Group("ClinicalWasteBin", t, new Vector3(0.62f, 0, -0.25f)).transform;
            Cyl("Body", clinical, new Vector3(0, 0.3f, 0), 0.3f, 0.6f, M("Yellow"));
            NoCol(Cyl("Lid", clinical, new Vector3(0, 0.61f, 0), 0.31f, 0.03f, M("Yellow")));
            NoCol(Box("Pedal", clinical, new Vector3(0, 0.03f, -0.17f), new Vector3(0.1f, 0.02f, 0.06f), M("DarkMetal")));
            var general = Group("GeneralWasteBin", t, new Vector3(-0.62f, 0, -0.25f)).transform;
            Cyl("Body", general, new Vector3(0, 0.3f, 0), 0.3f, 0.6f, M("DarkMetal"));
            NoCol(Cyl("Lid", general, new Vector3(0, 0.61f, 0), 0.31f, 0.03f, M("Black")));
        }

        static void BuildWhiteboard(Transform t)
        {
            Box("Board", t, new Vector3(0, 1.5f, -0.012f), new Vector3(1.2f, 0.8f, 0.02f), M("Sheet"));
            NoCol(Box("Frame_T", t, new Vector3(0, 1.91f, -0.015f), new Vector3(1.24f, 0.03f, 0.03f), M("Metal")));
            NoCol(Box("Frame_B", t, new Vector3(0, 1.09f, -0.015f), new Vector3(1.24f, 0.03f, 0.03f), M("Metal")));
            NoCol(Box("Frame_L", t, new Vector3(-0.61f, 1.5f, -0.015f), new Vector3(0.03f, 0.8f, 0.03f), M("Metal")));
            NoCol(Box("Frame_R", t, new Vector3(0.61f, 1.5f, -0.015f), new Vector3(0.03f, 0.8f, 0.03f), M("Metal")));
            NoCol(Box("MarkerTray", t, new Vector3(0, 1.08f, -0.05f), new Vector3(0.5f, 0.02f, 0.06f), M("Metal")));
            Label("Header", t, new Vector3(0, 1.8f, -0.025f), new Vector2(1.1f, 0.12f), "<b>ROOM 204  ·  BED 1</b>", new Color(0.1f, 0.35f, 0.45f), TextAlignmentOptions.Left);
            Label("Body", t, new Vector3(0, 1.45f, -0.025f), new Vector2(1.1f, 0.5f),
                "Your nurse today: ______\nPlan: Blood tests this morning\nDiet: Normal\nMobility: Assist x1\nFalls risk: Low",
                new Color(0.15f, 0.15f, 0.2f), TextAlignmentOptions.TopLeft);
        }

        static void BuildClock(Transform t)
        {
            var c = Group("Clock", t, new Vector3(0, 2.25f, -0.02f)).transform;
            NoCol(Cyl("Rim", c, new Vector3(0, 0, 0.005f), 0.33f, 0.035f, M("DarkMetal"), new Vector3(90, 0, 0)));
            NoCol(Cyl("Face", c, new Vector3(0, 0, -0.008f), 0.3f, 0.02f, M("Sheet"), new Vector3(90, 0, 0)));
            for (int i = 0; i < 12; i++)
            {
                var tick = Group("Tick_" + i, c, Vector3.zero);
                tick.transform.localRotation = Quaternion.Euler(0, 0, i * 30);
                NoCol(Box("Mark", tick.transform, new Vector3(0, 0.125f, -0.02f), new Vector3(i % 3 == 0 ? 0.012f : 0.006f, 0.025f, 0.003f), M("Black")));
            }
            Transform Hand(string n, float len, float w, Material m, float z)
            {
                var pivot = Group(n, c, new Vector3(0, 0, z)).transform;
                NoCol(Box("Hand", pivot, new Vector3(0, len / 2 - 0.015f, 0), new Vector3(w, len, 0.003f), m));
                return pivot;
            }
            var clock = c.gameObject.AddComponent<WallClock>();
            clock.hourHand = Hand("HourHand", 0.08f, 0.012f, M("Black"), -0.022f);
            clock.minuteHand = Hand("MinuteHand", 0.12f, 0.008f, M("Black"), -0.025f);
            clock.secondHand = Hand("SecondHand", 0.13f, 0.003f, M("Red"), -0.028f);
            NoCol(Cyl("Cap", c, new Vector3(0, 0, -0.03f), 0.015f, 0.006f, M("Red"), new Vector3(90, 0, 0)));
        }

        // ------------------------------------------------------------------ equipment

        static void BuildVitalsMonitor(Transform t)
        {
            NoCol(Box("WallPlate", t, new Vector3(0, 1.75f, -0.01f), new Vector3(0.1f, 0.14f, 0.02f), M("DarkMetal")));
            NoCol(Box("Arm", t, new Vector3(0, 1.75f, -0.15f), new Vector3(0.04f, 0.04f, 0.26f), M("DarkMetal")));

            var mon = Group("VitalsMonitor", t, new Vector3(0, 1.75f, -0.31f));
            mon.transform.localRotation = Quaternion.Euler(-10, 15, 0);
            var m = mon.transform;
            Box("Body", m, Vector3.zero, new Vector3(0.4f, 0.31f, 0.07f), M("MonitorBody"));
            NoCol(Box("Screen", m, new Vector3(0, 0.01f, -0.036f), new Vector3(0.35f, 0.24f, 0.004f), M("Screen")));
            NoCol(Box("Handle", m, new Vector3(0, 0.17f, 0), new Vector3(0.2f, 0.025f, 0.04f), M("MonitorBody")));
            NoCol(Box("StatusLED", m, new Vector3(0.17f, -0.135f, -0.036f), new Vector3(0.012f, 0.006f, 0.004f), M("EmissiveGreen")));

            float z = -0.039f;
            // ECG trace.
            var ecg = Group("ECGTrace", m, new Vector3(0, 0, z)).transform;
            Vector2[] beat = { new(0, 0), new(0.02f, 0), new(0.027f, 0.008f), new(0.034f, 0), new(0.042f, 0), new(0.046f, -0.006f), new(0.052f, 0.04f), new(0.058f, -0.012f), new(0.064f, 0), new(0.078f, 0.01f), new(0.09f, 0), new(0.1f, 0) };
            var pts = new List<Vector3>();
            for (int b = 0; b < 2; b++)
                foreach (var v in beat) pts.Add(new Vector3(-0.16f + b * 0.1f + v.x, 0.075f + v.y, 0));
            for (int i = 0; i < pts.Count - 1; i++) Segment("Seg", ecg, pts[i], pts[i + 1], 0.0025f, M("EmissiveGreen"));

            var green = new Color(0.35f, 1f, 0.45f);
            var red = new Color(1f, 0.45f, 0.45f);
            var cyan = new Color(0.4f, 0.9f, 1f);
            var yellow = new Color(1f, 0.9f, 0.35f);
            Label("Label_HR", m, new Vector3(0.1f, 0.1f, z), new Vector2(0.1f, 0.025f), "HR bpm", green, TextAlignmentOptions.Left);
            Label("Monitor_HR", m, new Vector3(0.11f, 0.06f, z), new Vector2(0.1f, 0.06f), "78", green, TextAlignmentOptions.Right);
            Label("Label_SpO2", m, new Vector3(0.1f, 0.02f, z), new Vector2(0.1f, 0.025f), "SpO<sub>2</sub> %", cyan, TextAlignmentOptions.Left);
            Label("Monitor_SpO2", m, new Vector3(0.11f, -0.015f, z), new Vector2(0.1f, 0.05f), "98", cyan, TextAlignmentOptions.Right);
            Label("Label_NIBP", m, new Vector3(-0.08f, 0.02f, z), new Vector2(0.15f, 0.025f), "NIBP mmHg", red, TextAlignmentOptions.Left);
            Label("Monitor_NIBP", m, new Vector3(-0.07f, -0.025f, z), new Vector2(0.17f, 0.06f), "---/---", red, TextAlignmentOptions.Left);
            Label("Monitor_NIBPTime", m, new Vector3(-0.08f, -0.065f, z), new Vector2(0.15f, 0.02f), "not measured", red, TextAlignmentOptions.Left);
            Label("Label_RR", m, new Vector3(0.1f, -0.06f, z), new Vector2(0.1f, 0.02f), "RR", yellow, TextAlignmentOptions.Left);
            Label("Monitor_RR", m, new Vector3(0.11f, -0.085f, z), new Vector2(0.1f, 0.035f), "16", yellow, TextAlignmentOptions.Right);
        }

        static void BuildBpCuffHolder(Transform t)
        {
            NoCol(Box("Basket", t, new Vector3(0, 1.2f, -0.06f), new Vector3(0.22f, 0.12f, 0.11f), M("PlasticGray")));
            var cuff = Box("BP_Cuff", t, new Vector3(0, 1.27f, -0.06f), new Vector3(0.17f, 0.1f, 0.07f), M("CuffBlue"));
            NoCol(Box("CuffStrap", cuff.transform.parent, new Vector3(0, 1.27f, -0.097f), new Vector3(0.12f, 0.06f, 0.004f), M("Sheet")));
            // Hose from cuff up to the monitor.
            Segment("Hose_A", t, new Vector3(0.05f, 1.3f, -0.08f), new Vector3(0.2f, 1.45f, -0.2f), 0.012f, M("Black"));
            Segment("Hose_B", t, new Vector3(0.2f, 1.45f, -0.2f), new Vector3(0.38f, 1.62f, -0.3f), 0.012f, M("Black"));
        }

        static void BuildIvPole(Transform t)
        {
            Star(t, 0.05f, 5, 0.25f, M("Metal"));
            Cyl("Pole", t, new Vector3(0, 1.0f, 0), 0.025f, 1.9f, M("Metal"));
            NoCol(Box("HookBar", t, new Vector3(0, 1.95f, 0), new Vector3(0.3f, 0.01f, 0.01f), M("Metal")));
            var bag = Box("IVBag", t, new Vector3(0.12f, 1.82f, 0), new Vector3(0.12f, 0.2f, 0.035f), M("IVBag"));
            NoCol(Box("IVBagLabel", t, new Vector3(0.12f, 1.83f, -0.019f), new Vector3(0.07f, 0.07f, 0.002f), M("Sheet")));
            NoCol(Cyl("DripChamber", t, new Vector3(0.12f, 1.68f, 0), 0.02f, 0.06f, M("Glass")));
            Segment("Line_A", t, new Vector3(0.12f, 1.65f, 0), new Vector3(0.06f, 1.27f, -0.04f), 0.005f, M("Glass"));
            var pump = Group("InfusionPump", t, new Vector3(0.0f, 1.2f, -0.06f)).transform;
            Box("Body", pump, Vector3.zero, new Vector3(0.14f, 0.18f, 0.1f), M("PlasticWhite"));
            NoCol(Box("Screen", pump, new Vector3(0, 0.04f, -0.051f), new Vector3(0.1f, 0.05f, 0.003f), M("ScreenBlue")));
            NoCol(Box("Keys", pump, new Vector3(0, -0.04f, -0.051f), new Vector3(0.1f, 0.06f, 0.003f), M("PlasticGray")));
            Label("Rate", pump, new Vector3(0, 0.04f, -0.054f), new Vector2(0.09f, 0.04f), "125 mL/h", new Color(0.8f, 0.95f, 1f), TextAlignmentOptions.Center);
        }

        static void BuildCommsTerminal(Transform t)
        {
            Box("Housing", t, new Vector3(0, 1.45f, -0.025f), new Vector3(0.48f, 0.34f, 0.05f), M("MonitorBody"));
            NoCol(Box("Screen", t, new Vector3(0, 1.45f, -0.052f), new Vector3(0.42f, 0.26f, 0.004f), M("ScreenBlue")));
            Label("Title", t, new Vector3(0, 1.54f, -0.056f), new Vector2(0.38f, 0.05f), "<b>WARD 2B · COMMS</b>", Color.white, TextAlignmentOptions.Center);
            Label("Body", t, new Vector3(0, 1.43f, -0.056f), new Vector2(0.38f, 0.14f), "Lab request\nTransport\nCall clinician", new Color(0.8f, 0.92f, 1f), TextAlignmentOptions.Center);
            NoCol(Box("Handset", t, new Vector3(0.3f, 1.45f, -0.04f), new Vector3(0.06f, 0.2f, 0.05f), M("Black")));
        }

        static void BuildProcedureTrolley(Transform t)
        {
            // Local +Z faces the bed (world +X).
            foreach (var sx in new[] { -1, 1 })
            foreach (var sz in new[] { -1, 1 })
            {
                Cyl("Post", t, new Vector3(sx * 0.33f, 0.5f, sz * 0.2f), 0.025f, 0.84f, M("Metal"));
                NoCol(Cyl("Caster", t, new Vector3(sx * 0.33f, 0.04f, sz * 0.2f), 0.07f, 0.03f, M("Black"), new Vector3(0, 0, 90)));
            }
            Box("TopShelf", t, new Vector3(0, 0.92f, 0), new Vector3(0.74f, 0.03f, 0.48f), M("PlasticWhite"));
            NoCol(Box("Lip_Back", t, new Vector3(0, 0.95f, -0.235f), new Vector3(0.74f, 0.04f, 0.01f), M("PlasticWhite")));
            NoCol(Box("Lip_L", t, new Vector3(-0.365f, 0.95f, 0), new Vector3(0.01f, 0.04f, 0.48f), M("PlasticWhite")));
            NoCol(Box("Lip_R", t, new Vector3(0.365f, 0.95f, 0), new Vector3(0.01f, 0.04f, 0.48f), M("PlasticWhite")));
            Box("MidShelf", t, new Vector3(0, 0.5f, 0), new Vector3(0.7f, 0.02f, 0.45f), M("PlasticGray"));
            Box("LowShelf", t, new Vector3(0, 0.15f, 0), new Vector3(0.7f, 0.02f, 0.45f), M("PlasticGray"));
            Box("DrawerUnit", t, new Vector3(0.14f, 0.72f, 0), new Vector3(0.4f, 0.36f, 0.42f), M("PlasticWhite"));
            string[] drawerMats = { "Accent", "Yellow", "GreenO2" };
            for (int i = 0; i < 3; i++)
                NoCol(Box("DrawerFront_" + i, t, new Vector3(0.14f, 0.84f - i * 0.115f, 0.212f), new Vector3(0.36f, 0.1f, 0.006f), M(drawerMats[i])));
            NoCol(Cyl("PushHandle", t, new Vector3(0, 1.0f, -0.27f), 0.025f, 0.6f, M("Metal"), new Vector3(0, 0, 90)));

            float top = 0.935f;
            var items = Group("Supplies", t, new Vector3(0, top, 0)).transform;
            Box("ProcedureTray", items, new Vector3(-0.12f, 0.01f, 0), new Vector3(0.4f, 0.02f, 0.3f), M("TrayBlue"));

            var rack = Group("TubeRack", items, new Vector3(-0.17f, 0.02f, 0.08f)).transform;
            Box("Rack", rack, new Vector3(0, 0.025f, 0), new Vector3(0.22f, 0.05f, 0.06f), M("PlasticWhite"));
            string[] caps = { "CapLavender", "CapGold", "CapLightBlue", "CapGreen" };
            string[] names = { "Tube_EDTA_Lavender", "Tube_SST_Gold", "Tube_Citrate_LightBlue", "Tube_Heparin_Green" };
            for (int i = 0; i < 4; i++)
            {
                var tube = Group(names[i], rack, new Vector3(-0.075f + i * 0.05f, 0.06f, 0)).transform;
                Cyl("Body", tube, Vector3.zero, 0.016f, 0.1f, M("Glass"));
                Cyl("Cap", tube, new Vector3(0, 0.058f, 0), 0.02f, 0.022f, M(caps[i]));
            }

            Box("Tourniquet", items, new Vector3(-0.08f, 0.024f, -0.08f), new Vector3(0.26f, 0.005f, 0.025f), M("CuffBlue"));
            for (int i = 0; i < 3; i++)
                Box("AlcoholSwab_" + i, items, new Vector3(0.0f + i * 0.0f, 0.024f + i * 0.004f, 0.0f + i * 0.004f), new Vector3(0.045f, 0.004f, 0.045f), M("SwabPacket"));
            Box("Gauze", items, new Vector3(-0.24f, 0.026f, -0.08f), new Vector3(0.06f, 0.01f, 0.06f), M("Sheet"));
            Box("Plasters", items, new Vector3(-0.27f, 0.03f, 0.01f), new Vector3(0.05f, 0.02f, 0.07f), M("Terracotta"));

            var holder = Group("VacutainerHolder", items, new Vector3(0.0f, 0.03f, -0.1f)).transform;
            holder.localRotation = Quaternion.Euler(0, 0, 90);
            Cyl("Barrel", holder, Vector3.zero, 0.022f, 0.08f, M("Glass"));
            Cyl("Needle", holder, new Vector3(0, -0.055f, 0), 0.003f, 0.035f, M("Metal"));
            Cyl("NeedleCap", holder, new Vector3(0, -0.06f, 0), 0.01f, 0.04f, M("CapGreen"));

            var sharps = Group("SharpsContainer", items, new Vector3(0.24f, 0, 0.12f)).transform;
            Box("Body", sharps, new Vector3(0, 0.09f, 0), new Vector3(0.15f, 0.18f, 0.13f), M("Yellow"));
            Box("Lid", sharps, new Vector3(0, 0.19f, 0), new Vector3(0.155f, 0.02f, 0.135f), M("Red"));
            Label("Label", sharps, new Vector3(0, 0.1f, 0.066f), new Vector2(0.12f, 0.05f), "<b>SHARPS</b>", Color.black, TextAlignmentOptions.Center, new Vector3(0, 180, 0));

            Box("SpecimenBag", items, new Vector3(0.24f, 0.006f, -0.1f), new Vector3(0.15f, 0.006f, 0.2f), M("SpecimenBag"));

            var handheld = Group("HandheldDevice", items, new Vector3(0.05f, 0.01f, 0.13f)).transform;
            handheld.localRotation = Quaternion.Euler(90, 90, 0);
            Box("Body", handheld, Vector3.zero, new Vector3(0.08f, 0.15f, 0.015f), M("MonitorBody"));
            NoCol(Box("Screen", handheld, new Vector3(0, 0.01f, -0.0078f), new Vector3(0.07f, 0.11f, 0.002f), M("ScreenBlue")));

            var printer = Group("LabelPrinter", t, new Vector3(-0.12f, 0.51f, 0.0f)).transform;
            Box("Body", printer, new Vector3(0, 0.04f, 0), new Vector3(0.12f, 0.08f, 0.15f), M("MonitorBody"));
            NoCol(Box("LabelStrip", printer, new Vector3(0, 0.03f, 0.08f), new Vector3(0.06f, 0.002f, 0.03f), M("Sheet")));
        }

        // ------------------------------------------------------------------ patient

        static void BuildPatient(Transform bed, Transform headPivot)
        {
            var root = Group("Patient", bed, Vector3.zero).transform;
            var upper = Group("UpperBody", headPivot, Vector3.zero).transform;

            var torso = Cap("Torso", upper, new Vector3(0, 0.215f, 0.2f), 1f, 1f, M("Gown"), new Vector3(90, 0, 0));
            torso.transform.localScale = new Vector3(0.4f, 0.33f, 0.24f);
            NoCol(Sph("Shoulder_R", upper, new Vector3(-0.2f, 0.2f, 0.45f), Vector3.one * 0.13f, M("Gown")));
            NoCol(Sph("Shoulder_L", upper, new Vector3(0.2f, 0.2f, 0.45f), Vector3.one * 0.13f, M("Gown")));
            NoCol(Cyl("Neck", upper, new Vector3(0, 0.24f, 0.56f), 0.1f, 0.1f, M("Skin"), new Vector3(90, 0, 0)));

            var head = Group("Head", upper, new Vector3(0, 0.3f, 0.66f)).transform;
            Sph("Skull", head, Vector3.zero, new Vector3(0.18f, 0.21f, 0.23f), M("Skin"));
            NoCol(Sph("Hair", head, new Vector3(0, -0.015f, 0.03f), new Vector3(0.195f, 0.2f, 0.22f), M("Hair")));
            NoCol(Sph("Nose", head, new Vector3(0, 0.105f, -0.005f), new Vector3(0.03f, 0.03f, 0.04f), M("Skin")));
            NoCol(Sph("Eye_R", head, new Vector3(-0.035f, 0.095f, 0.015f), new Vector3(0.025f, 0.008f, 0.012f), M("Hair")));
            NoCol(Sph("Eye_L", head, new Vector3(0.035f, 0.095f, 0.015f), new Vector3(0.025f, 0.008f, 0.012f), M("Hair")));
            NoCol(Box("Mouth", head, new Vector3(0, 0.092f, -0.05f), new Vector3(0.04f, 0.006f, 0.008f), M("Lips")));
            NoCol(Sph("Ear_R", head, new Vector3(-0.09f, 0, 0.01f), new Vector3(0.02f, 0.05f, 0.04f), M("Skin")));
            NoCol(Sph("Ear_L", head, new Vector3(0.09f, 0, 0.01f), new Vector3(0.02f, 0.05f, 0.04f), M("Skin")));

            // Upper arms ride the raised head section; forearms rest flat on the sheet (bed space).
            Limb("UpperArm_R", upper, new Vector3(-0.25f, 0.15f, 0.42f), new Vector3(-0.25f, 0.15f, 0.06f), 0.1f, M("Gown"));
            Limb("UpperArm_L", upper, new Vector3(0.25f, 0.15f, 0.42f), new Vector3(0.25f, 0.15f, 0.06f), 0.1f, M("Gown"));

            // Patient's right arm is on the nurse's (west) side: BP cuff, venipuncture and wristband go here.
            var rightArm = Group("RightArm", root, Vector3.zero).transform;
            Vector3 elbowR = new Vector3(-0.25f, 0.66f, 0.305f), wristR = new Vector3(-0.3f, 0.635f, 0.06f);
            Limb("Forearm_R", rightArm, elbowR, wristR, 0.085f, M("Skin"));
            NoCol(Sph("AntecubitalFossa_R", rightArm, elbowR + new Vector3(0, 0.03f, -0.02f), new Vector3(0.05f, 0.01f, 0.05f), M("Skin")));
            Sph("Hand_R", rightArm, new Vector3(-0.305f, 0.625f, 0.0f), new Vector3(0.08f, 0.035f, 0.11f), M("Skin"));

            var band = Group("Wristband", rightArm, Vector3.Lerp(wristR, elbowR, 0.08f)).transform;
            band.localRotation = Quaternion.LookRotation(elbowR - wristR) * Quaternion.Euler(90, 0, 0);
            NoCol(Cyl("Band", band, Vector3.zero, 0.098f, 0.026f, M("PlasticWhite")));
            var qr = Box("Wristband_QR", rightArm, band.localPosition + new Vector3(0, 0.05f, 0), new Vector3(0.036f, 0.002f, 0.036f), M("QR"));
            qr.transform.localRotation = Quaternion.Euler(0, Quaternion.LookRotation(elbowR - wristR).eulerAngles.y, 0);

            var leftArm = Group("LeftArm", root, Vector3.zero).transform;
            Vector3 elbowL = new Vector3(0.25f, 0.66f, 0.305f), wristL = new Vector3(0.3f, 0.635f, 0.06f);
            Limb("Forearm_L", leftArm, elbowL, wristL, 0.085f, M("Skin"));
            Sph("Hand_L", leftArm, new Vector3(0.305f, 0.625f, 0.0f), new Vector3(0.08f, 0.035f, 0.11f), M("Skin"));
        }

        // ------------------------------------------------------------------ lighting & player

        static void BuildLighting(Transform p)
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.66f, 0.72f);
            RenderSettings.ambientEquatorColor = new Color(0.52f, 0.52f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.28f, 0.27f, 0.26f);
            RenderSettings.fog = false;

            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(p, false);
            sunGo.transform.rotation = Quaternion.Euler(32, 255, 0);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.94f, 0.84f);
            sun.intensity = 1.6f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            RenderSettings.sun = sun;

            PointLight(p, "CeilingLight_Bed", new Vector3(BedX, RoomH - 0.3f, 1.0f), 1.4f, 5.5f);
            PointLight(p, "CeilingLight_Entry", new Vector3(-1.5f, RoomH - 0.3f, -1.2f), 1.1f, 5f);
            PointLight(p, "CeilingLight_Window", new Vector3(1.8f, RoomH - 0.3f, -1.0f), 0.6f, 4f);

            var spotGo = new GameObject("ReadingLight");
            spotGo.transform.SetParent(p, false);
            spotGo.transform.position = new Vector3(BedX, 2.0f, 2.35f);
            spotGo.transform.rotation = Quaternion.Euler(70, 180, 0);
            var spot = spotGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = new Color(1f, 0.9f, 0.78f);
            spot.intensity = 1.2f;
            spot.range = 3f;
            spot.spotAngle = 90f;
            spot.innerSpotAngle = 40f;
            spot.shadows = LightShadows.None;

            var probeGo = new GameObject("ReflectionProbe");
            probeGo.transform.SetParent(p, false);
            probeGo.transform.position = new Vector3(0, 1.4f, 0);
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.size = new Vector3(RoomW, RoomH, RoomD);
            probe.center = new Vector3(0, 0.05f, 0);
            probe.boxProjection = true;
            probe.resolution = 128;
        }

        static void PointLight(Transform p, string n, Vector3 pos, float intensity, float range)
        {
            var go = new GameObject(n);
            go.transform.SetParent(p, false);
            go.transform.position = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.96f, 0.9f);
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
        }

        static void BuildPlayer()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(XrRigPath);
            if (prefab == null)
            {
                Debug.LogWarning("[HSE] XR rig prefab not found at " + XrRigPath + ". Add the template's XR Origin manually.");
                return;
            }
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            rig.transform.position = new Vector3(-2.0f, 0, -1.7f);
            rig.transform.rotation = Quaternion.Euler(0, 35, 0);
        }

        static void AddTeleportArea(GameObject floor)
        {
            var type = System.Type.GetType("UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationArea, Unity.XR.Interaction.Toolkit");
            if (type != null) floor.AddComponent(type);
            else Debug.LogWarning("[HSE] TeleportationArea type not found; floor is not teleportable.");
        }

        // ------------------------------------------------------------------ materials & textures

        static void CreateMaterials()
        {
            var floorTex = MakeTexture("FloorVinyl", 256, (x, y, r) =>
            {
                bool line = x < 2 || y < 2;
                float n = (float)(r.NextDouble() - 0.5) * 0.035f;
                bool fleck = r.NextDouble() < 0.012;
                var c = new Color(0.78f, 0.79f, 0.75f) + new Color(n, n, n);
                if (fleck) c = Color.Lerp(c, new Color(0.45f, 0.55f, 0.55f), 0.6f);
                return line ? c * 0.86f : c;
            });
            var ceilingTex = MakeTexture("CeilingTile", 256, (x, y, r) =>
            {
                bool line = x < 3 || y < 3;
                bool dot = (x % 16 == 8) && (y % 16 == 8) && r.NextDouble() < 0.7;
                float n = (float)(r.NextDouble() - 0.5) * 0.02f;
                var c = new Color(0.95f, 0.95f, 0.94f) + new Color(n, n, n);
                if (dot) c *= 0.82f;
                return line ? new Color(0.78f, 0.79f, 0.8f) : c;
            });
            var tileTex = MakeTexture("WallTile", 128, (x, y, r) => (x % 64 < 2 || y % 64 < 2) ? new Color(0.75f, 0.76f, 0.76f) : new Color(0.93f, 0.95f, 0.95f));
            var qrTex = MakeQrTexture();

            Lit("Floor", Color.white, 0.45f, 0, floorTex, new Vector2((RoomW + 0.2f) / 0.6f, (RoomD + 0.2f) / 0.6f));
            Lit("Ceiling", Color.white, 0.1f, 0, ceilingTex, new Vector2((RoomW + 0.2f) / 0.6f, (RoomD + 0.2f) / 0.6f));
            Lit("Tile", Color.white, 0.6f, 0, tileTex, new Vector2(4, 2));
            Lit("QR", Color.white, 0.2f, 0, qrTex, Vector2.one);

            Lit("Wall", new Color(0.93f, 0.92f, 0.88f), 0.15f);
            Lit("Wainscot", new Color(0.6f, 0.77f, 0.73f), 0.35f);
            Lit("Skirting", new Color(0.33f, 0.37f, 0.39f), 0.3f);
            Lit("WoodLight", new Color(0.76f, 0.6f, 0.43f), 0.35f);
            Lit("Door", new Color(0.72f, 0.58f, 0.42f), 0.3f);
            Lit("Metal", new Color(0.8f, 0.81f, 0.83f), 0.65f, 0.85f);
            Lit("DarkMetal", new Color(0.24f, 0.25f, 0.27f), 0.5f, 0.6f);
            Lit("PlasticWhite", new Color(0.95f, 0.95f, 0.94f), 0.45f);
            Lit("PlasticGray", new Color(0.6f, 0.62f, 0.64f), 0.4f);
            Lit("MonitorBody", new Color(0.2f, 0.21f, 0.23f), 0.45f);
            Lit("Counter", new Color(0.82f, 0.82f, 0.8f), 0.55f);
            Lit("Mattress", new Color(0.36f, 0.5f, 0.62f), 0.35f);
            Lit("Sheet", new Color(0.97f, 0.97f, 0.98f), 0.1f);
            Lit("Blanket", new Color(0.42f, 0.64f, 0.69f), 0.05f);
            Lit("Gown", new Color(0.62f, 0.76f, 0.88f), 0.1f);
            Lit("Skin", new Color(0.86f, 0.68f, 0.56f), 0.3f);
            Lit("Hair", new Color(0.2f, 0.14f, 0.1f), 0.3f);
            Lit("Lips", new Color(0.7f, 0.4f, 0.38f), 0.3f);
            Lit("UpholsteryBlue", new Color(0.15f, 0.27f, 0.44f), 0.25f);
            Lit("UpholsteryTeal", new Color(0.2f, 0.48f, 0.5f), 0.2f);
            Lit("UpholsteryTealLight", new Color(0.32f, 0.6f, 0.6f), 0.2f);
            Lit("Curtain", new Color(0.56f, 0.73f, 0.82f), 0.05f);
            Lit("CurtainMesh", new Color(0.9f, 0.92f, 0.93f), 0.05f);
            Lit("Black", new Color(0.06f, 0.06f, 0.07f), 0.5f);
            Lit("Red", new Color(0.78f, 0.13f, 0.13f), 0.4f);
            Lit("Yellow", new Color(0.97f, 0.8f, 0.1f), 0.4f);
            Lit("GreenO2", new Color(0.1f, 0.58f, 0.3f), 0.45f);
            Lit("Accent", new Color(0.12f, 0.45f, 0.75f), 0.45f);
            Lit("TrayBlue", new Color(0.55f, 0.75f, 0.92f), 0.5f);
            Lit("CuffBlue", new Color(0.12f, 0.3f, 0.65f), 0.25f);
            Lit("GloveS", new Color(0.55f, 0.4f, 0.75f), 0.3f);
            Lit("GloveM", new Color(0.25f, 0.45f, 0.85f), 0.3f);
            Lit("GloveL", new Color(0.2f, 0.6f, 0.45f), 0.3f);
            Lit("SwabPacket", new Color(0.92f, 0.92f, 0.95f), 0.6f);
            Lit("CapLavender", new Color(0.62f, 0.5f, 0.86f), 0.4f);
            Lit("CapGold", new Color(0.92f, 0.72f, 0.2f), 0.5f);
            Lit("CapLightBlue", new Color(0.5f, 0.76f, 0.96f), 0.4f);
            Lit("CapGreen", new Color(0.2f, 0.65f, 0.3f), 0.4f);
            Lit("Terracotta", new Color(0.72f, 0.42f, 0.3f), 0.25f);
            Lit("Leaves", new Color(0.24f, 0.48f, 0.24f), 0.2f);
            Lit("Bark", new Color(0.35f, 0.26f, 0.2f), 0.1f);
            Lit("Grass", new Color(0.36f, 0.5f, 0.3f), 0.05f, 0, null, default, new Color(0.08f, 0.12f, 0.07f));
            Lit("Building", new Color(0.78f, 0.8f, 0.82f), 0.2f, 0, null, default, new Color(0.25f, 0.26f, 0.28f));
            Lit("BuildingWarm", new Color(0.84f, 0.78f, 0.7f), 0.2f, 0, null, default, new Color(0.27f, 0.25f, 0.22f));
            Lit("BuildingGlass", new Color(0.35f, 0.45f, 0.55f), 0.9f, 0, null, default, new Color(0.12f, 0.16f, 0.2f));
            Lit("Sky", new Color(0.72f, 0.84f, 0.96f), 0f, 0, null, default, new Color(0.72f, 0.84f, 0.96f));

            Lit("LightPanel", Color.white, 0.2f, 0, null, default, new Color(1f, 0.97f, 0.92f) * 2.2f);
            Lit("Screen", new Color(0.02f, 0.03f, 0.04f), 0.9f, 0, null, default, new Color(0.01f, 0.025f, 0.035f));
            Lit("ScreenBlue", new Color(0.05f, 0.15f, 0.3f), 0.9f, 0, null, default, new Color(0.04f, 0.14f, 0.3f));
            Lit("EmissiveGreen", new Color(0.3f, 1f, 0.4f), 0.5f, 0, null, default, new Color(0.3f, 1f, 0.4f) * 1.5f);

            Transparent("Glass", new Color(0.82f, 0.9f, 0.96f, 0.15f), 0.95f);
            Transparent("DarkGlass", new Color(0.2f, 0.25f, 0.3f, 0.6f), 0.95f);
            Transparent("IVBag", new Color(0.9f, 0.95f, 1f, 0.45f), 0.8f);
            Transparent("SpecimenBag", new Color(0.95f, 0.95f, 0.98f, 0.4f), 0.7f);
        }

        static Material M(string name) => Mats.TryGetValue(name, out var m) ? m : Mats["PlasticGray"];

        static Material LoadOrCreate(string name)
        {
            string path = $"{MatDir}/{name}.mat";
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }
            else m.shader = shader;
            Mats[name] = m;
            return m;
        }

        static void Lit(string name, Color c, float smooth, float metal = 0, Texture2D tex = null, Vector2 tiling = default, Color emission = default)
        {
            var m = LoadOrCreate(name);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            if (tex != null)
            {
                m.SetTexture("_BaseMap", tex);
                m.SetTextureScale("_BaseMap", tiling == default ? Vector2.one : tiling);
            }
            if (emission.maxColorComponent > 0)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
            }
            EditorUtility.SetDirty(m);
        }

        static void Transparent(string name, Color c, float smooth)
        {
            var m = LoadOrCreate(name);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", 0);
            m.SetFloat("_Surface", 1);
            m.SetFloat("_Blend", 0);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(m);
        }

        static Texture2D MakeTexture(string name, int size, System.Func<int, int, System.Random, Color> pixel)
        {
            var rnd = new System.Random(name.GetHashCode());
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, pixel(x, y, rnd));
            return SaveTexture(name, tex, FilterMode.Bilinear, TextureWrapMode.Repeat);
        }

        static Texture2D MakeQrTexture()
        {
            // Visual stand-in for the wristband code; scanning is resolved in-game by collider, not by decoding.
            const int modules = 25, px = 10, quiet = 2;
            int size = (modules + quiet * 2) * px;
            var rnd = new System.Random(20401);
            var grid = new bool[modules, modules];
            for (int y = 0; y < modules; y++)
            for (int x = 0; x < modules; x++)
                grid[x, y] = rnd.NextDouble() < 0.5;
            void Finder(int ox, int oy)
            {
                for (int y = -1; y < 8; y++)
                for (int x = -1; x < 8; x++)
                {
                    int gx = ox + x, gy = oy + y;
                    if (gx < 0 || gy < 0 || gx >= modules || gy >= modules) continue;
                    bool ring = x == 0 || x == 6 || y == 0 || y == 6;
                    bool core = x >= 2 && x <= 4 && y >= 2 && y <= 4;
                    bool inside = x >= 0 && x <= 6 && y >= 0 && y <= 6;
                    grid[gx, gy] = inside && (ring || core);
                }
            }
            Finder(0, 0); Finder(modules - 7, 0); Finder(0, modules - 7);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int mx = x / px - quiet, my = y / px - quiet;
                bool dark = mx >= 0 && my >= 0 && mx < modules && my < modules && grid[mx, my];
                tex.SetPixel(x, y, dark ? Color.black : Color.white);
            }
            return SaveTexture("WristbandQR", tex, FilterMode.Point, TextureWrapMode.Clamp);
        }

        static Texture2D SaveTexture(string name, Texture2D tex, FilterMode filter, TextureWrapMode wrap)
        {
            tex.Apply();
            string path = $"{TexDir}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) is TextureImporter imp)
            {
                imp.filterMode = filter;
                imp.wrapMode = wrap;
                imp.mipmapEnabled = filter != FilterMode.Point;
                imp.anisoLevel = 4;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ------------------------------------------------------------------ primitive helpers

        static GameObject Group(string name, Transform parent, Vector3 localPos, float yaw = 0)
        {
            var go = new GameObject(name);
            if (parent) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            return go;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material m, Vector3? euler)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            if (m.renderQueue >= (int)RenderQueue.Transparent) r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        static GameObject Box(string n, Transform p, Vector3 pos, Vector3 size, Material m, Vector3? euler = null) => Prim(PrimitiveType.Cube, n, p, pos, size, m, euler);
        static GameObject Sph(string n, Transform p, Vector3 pos, Vector3 size, Material m) => Prim(PrimitiveType.Sphere, n, p, pos, size, m, null);
        static GameObject Cyl(string n, Transform p, Vector3 pos, float diameter, float height, Material m, Vector3? euler = null) => Prim(PrimitiveType.Cylinder, n, p, pos, new Vector3(diameter, height / 2, diameter), m, euler);
        static GameObject Cap(string n, Transform p, Vector3 pos, float diameter, float height, Material m, Vector3? euler = null) => Prim(PrimitiveType.Capsule, n, p, pos, new Vector3(diameter, height / 2, diameter), m, euler);

        static GameObject Limb(string n, Transform p, Vector3 a, Vector3 b, float diameter, Material m)
        {
            var go = Cap(n, p, (a + b) / 2, diameter, Vector3.Distance(a, b) + diameter, m);
            go.transform.localRotation = Quaternion.LookRotation(b - a) * Quaternion.Euler(90, 0, 0);
            return go;
        }

        static void Segment(string n, Transform p, Vector3 a, Vector3 b, float thickness, Material m)
        {
            var go = Box(n, p, (a + b) / 2, new Vector3(thickness, thickness, Vector3.Distance(a, b) + thickness * 0.5f), m);
            go.transform.localRotation = Quaternion.LookRotation(b - a == Vector3.zero ? Vector3.forward : b - a, Vector3.up);
            NoCol(go);
        }

        static void Star(Transform p, float y, int legs, float length, Material m)
        {
            for (int i = 0; i < legs; i++)
            {
                var leg = Group("Leg_" + i, p, Vector3.zero, i * 360f / legs).transform;
                NoCol(Box("Spoke", leg, new Vector3(0, y, length / 2), new Vector3(0.04f, 0.03f, length), m));
                NoCol(Sph("Caster", leg, new Vector3(0, 0.03f, length), Vector3.one * 0.055f, M("Black")));
            }
        }

        static GameObject NoCol(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c) Object.DestroyImmediate(c);
            return go;
        }

        static TextMeshPro Label(string n, Transform p, Vector3 pos, Vector2 size, string text, Color color, TextAlignmentOptions align, Vector3? euler = null)
        {
            var go = new GameObject(n);
            go.transform.SetParent(p, false);
            var tmp = go.AddComponent<TextMeshPro>();
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            tmp.rectTransform.sizeDelta = size;
            tmp.text = text;
            tmp.color = color;
            tmp.alignment = align;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.01f;
            tmp.fontSizeMax = 10f;
            tmp.margin = Vector4.zero;
            return tmp;
        }

        // ------------------------------------------------------------------ misc

        static void MarkStatic(GameObject root)
        {
            var flags = StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic |
                        StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.GetComponent<WallClock>() == null && t.GetComponentInParent<WallClock>() == null)
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, flags);
        }

        static void AddSceneToBuildSettings(string path)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == path);
            scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
