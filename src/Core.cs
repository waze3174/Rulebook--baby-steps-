using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using HarmonyLib;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

[assembly: MelonInfo(typeof(Rulebook.Core), "Rulebook", "1.2.1", "waze3174")]
[assembly: MelonGame]
[assembly: MelonOptionalDependencies("BabyStepsMenuLib", "BabyStepsMultiplayerClient", "BabyStepsNetworking")]

namespace Rulebook;

// your own rules for baby steps: grip, how much it takes to fall, the splits, gravity, fatigue, wind, and the size of each part of nate.
// set from the pause menu (with BabyStepsMenuLib) or UserData/Rulebook/Rulebook.cfg; every dial at its normal value = the normal game.
// while anything isn't normal, a list down the left side of the screen shows it (can be turned off)
public class Core : MelonMod
{
    internal static Core Inst;
    internal static MelonPreferences_Category Cat;
    internal static MelonPreferences_Entry<bool> ShowTag, KeepItems, KeepHat;

    // one dial per rule
    internal class Dial
    {
        public string Key, Name, Tag, Tab, Note;
        public float Normal, Min, Max;
        public bool Percent;
        public bool Look;   // looks, not a rule: left off the rules list
        public MelonPreferences_Entry<float> Entry;
        public float Value => Mathf.Clamp(Entry.Value, Min, Max);
        public bool IsNormal => Mathf.Abs(Entry.Value - Normal) < 0.001f;
        public string Show(float v) => Percent ? (v * 100f).ToString("0", CultureInfo.InvariantCulture) + "%" : Normal == 0f ? v.ToString("0.0", CultureInfo.InvariantCulture) : "×" + v.ToString("0.00", CultureInfo.InvariantCulture);
    }

    internal static readonly List<Dial> Dials = new();
    internal static Dial Grip, Lean, Balance, Landings, Splits, Gravity, Fatigue, Wind, Head, UpperArms, Forearms, Hands, Torso, Thighs, Shins, Feet, Whole, Hips, Red, Green, Blue;

    static Dial Add(string key, string name, string tag, string tab, float normal, float min, float max, string note, bool percent = false)
    {
        var d = new Dial { Key = key, Name = name, Tag = tag, Tab = tab, Normal = normal, Min = min, Max = max, Note = note, Percent = percent };
        d.Entry = Cat.CreateEntry(key, normal, name, $"{note} Normal = {d.Show(normal)}, {d.Show(min)} to {d.Show(max)}.");
        Dials.Add(d);
        return d;
    }

    public override void OnInitializeMelon()
    {
        Inst = this;
        string dir = Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "Rulebook");
        Directory.CreateDirectory(dir);
        Cat = MelonPreferences.CreateCategory("Rulebook");
        // carried over from the mod's working name
        string old = Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "AssistMode", "AssistMode.cfg");
        string cfg = Path.Combine(dir, "Rulebook.cfg");
        if (!File.Exists(cfg) && File.Exists(old))
            try { File.WriteAllText(cfg, File.ReadAllText(old).Replace("[AssistMode]", "[Rulebook]")); } catch { }
        Cat.SetFilePath(cfg);

        Grip = Add("Grip", "Grip", "GRIP", "World", 1f, 0.02f, 10f, "Grip on every surface. 0.15 = everything is ice.");
        Lean = Add("LeanLimit", "Lean limit", "LEAN", "Falls", 1f, 0.5f, 3f, "How far you can lean before you go over (the game's 30/40/55 degree limits, kept under 85).");
        Balance = Add("BalanceTime", "Balance time", "BALANCE", "Falls", 1f, 0.2f, 10f, "How long you can stay off balance or squashed before you fall.");
        Landings = Add("HardLandings", "Hard landings", "LANDINGS", "Falls", 1f, 0.2f, 10f, "How hard a hit you can take to the hips.");
        Splits = Add("Splits", "Splits", "SPLITS", "Falls", 1f, 0.5f, 3f, "How far apart your feet can be before the splits drop you. Above 1 very squashed legs also drop you a bit sooner, below 1 a bit later.");
        Gravity = Add("Gravity", "Gravity", "GRAVITY", "World", 1f, 0.05f, 5f, "Gravity for nate (standing, walking, lifting his feet, falling) and the world.");
        Fatigue = Add("Fatigue", "Fatigue", "FATIGUE", "World", 1f, 0f, 10f, "How fast legs tire. 0 = never.", percent: true);
        Wind = Add("Wind", "Wind", "WIND", "World", 0f, 0f, 50f, "Gusty wind pushing you around. 0 = none.");
        // body: each bone in both of nate's skeletons (ragdoll = physics, and the visible one); left and right together
        Head = Add("HeadSize", "Head", "HEAD", "Arms", 1f, 0.3f, 3f, "Nate's head.");
        UpperArms = Add("UpperArmSize", "Upper arms", "UPPER ARMS", "Arms", 1f, 0.3f, 3f, "Nate's upper arms.");
        Forearms = Add("ForearmSize", "Forearms", "FOREARMS", "Arms", 1f, 0.3f, 3f, "Nate's forearms.");
        Hands = Add("HandSize", "Hands", "HANDS", "Arms", 1f, 0.3f, 3f, "Nate's hands.");
        Torso = Add("TorsoSize", "Torso", "TORSO", "Legs", 1f, 0.5f, 1.15f, "Nate's torso (pushes the head and arms out, they keep their own size). x0.5 to x1.15: a bigger one tips him over.");
        Thighs = Add("ThighSize", "Thighs", "THIGHS", "Legs", 1f, 0.5f, 1.15f, "Nate's thighs. Longer legs walk taller with longer steps. Between x0.5 and x1.15, like all of nate: the game's walking can't stand on longer or shorter legs.");
        Shins = Add("ShinSize", "Shins", "SHINS", "Legs", 1f, 0.5f, 1.15f, "Nate's shins. Longer legs walk taller with longer steps. Between x0.5 and x1.15, like all of nate: the game's walking can't stand on longer or shorter legs.");
        Feet = Add("FeetSize", "Feet", "FEET", "Legs", 1f, 0.5f, 1.15f, "Nate's feet, and the sole he balances on. x0.5 to x1.15, the walking falls apart past that.");
        Whole = Add("NateSize", "Whole Nate", "SIZE", "Size", 1f, 0.5f, 1.15f, "All of nate at once, on top of the other body dials: legs, feet, his walking and the camera. The game's walking has distances built in, so he only plays from x0.5 to x1.15.");
        Hips = Add("HipSize", "Hips & butt", "HIPS", "Size", 1f, 0.5f, 1.15f, "Nate's pelvis with his butt and belly.");
        // suit colour like the multiplayer mod's, offline too. all at 100% = the game's own suit
        Red = Add("SuitRed", "Suit red", "RED", "Misc", 1f, 0f, 1f, "Red in nate's suit colour (multiplayer's own colour takes over while you're on a server).", percent: true);
        Green = Add("SuitGreen", "Suit green", "GREEN", "Misc", 1f, 0f, 1f, "Green in nate's suit colour.", percent: true);
        Blue = Add("SuitBlue", "Suit blue", "BLUE", "Misc", 1f, 0f, 1f, "Blue in nate's suit colour.", percent: true);
        Red.Look = Green.Look = Blue.Look = true;
        ShowTag = Cat.CreateEntry("ShowTag", false, "Show tag", "List the rules that aren't normal down the left side of the screen.");
        KeepItems = Cat.CreateEntry("KeepItems", false, "Keep items", "Nate never drops what he holds when he falls or bumps his hands (swapping items still works).");
        KeepHat = Cat.CreateEntry("KeepHat", false, "Keep hat", "Nate's hat never gets knocked off when he falls (changing hats still works).");

        // 0.1/0.2 had one "FallTolerance" dial for lean, balance and landings together
        var oldTol = Cat.CreateEntry("FallTolerance", 1f, "Fall tolerance (old)", "Replaced by LeanLimit, BalanceTime and HardLandings.", is_hidden: true);
        if (Mathf.Abs(oldTol.Value - 1f) > 0.001f)
        {
            Lean.Entry.Value = Mathf.Min(oldTol.Value, Lean.Max); Balance.Entry.Value = oldTol.Value; Landings.Entry.Value = oldTol.Value;
            oldTol.Value = 1f;
        }
        Changed();
        // our tabs for the pause menu page (needs BabyStepsMenuLib); built once every mod has started
        try { RegisterMenu(); menuOk = true; }
        catch (Exception e) when (e is FileNotFoundException || e is TypeLoadException || e is MissingMethodException) { }
    }

    static void RegisterMenu() => RulebookMenu.Announce();
    void BuildMenu() => RulebookMenu.Build(LoggerInstance, HarmonyInstance);
    static void HubLateUpdate() => MenuHub.LateUpdate();

    public override void OnLateInitializeMelon()
    {
        // after every mod has started: one page for all my mods, the library's hooks added only if nobody did
        if (!menuOk) { LoggerInstance.Msg("BabyStepsMenuLib not installed: settings only in UserData/Rulebook/Rulebook.cfg"); return; }
        try { BuildMenu(); }
        catch (Exception e) when (e is FileNotFoundException || e is TypeLoadException || e is MissingMethodException)
        { LoggerInstance.Msg("BabyStepsMenuLib not installed: settings only in UserData/Rulebook/Rulebook.cfg"); }
    }

    internal static void Changed() => Cat.SaveToFile(false);

    // an error that comes back every frame or step goes in the log once
    static readonly HashSet<string> told = new();
    static void Once(string what, Exception e)
    {
        if (told.Add(what)) Inst.LoggerInstance.Error($"{what}: {e}");
    }

    internal static void ResetAll()
    {
        foreach (var d in Dials) d.Entry.Value = d.Normal;
        KeepItems.Value = false; KeepHat.Value = false;
        Changed();
    }

    // ---- grip: every traction lookup the feet make ----

    [HarmonyPatch(typeof(Il2Cpp.TractionByteKeeper), nameof(Il2Cpp.TractionByteKeeper.GetTractionAt))]
    static class GripPatch
    {
        static void Postfix(ref float __result) => __result *= Grip.Value;
    }

    // ---- splits: the game checks feet distance against 1.95 leg lengths; stretch the leg length it sees while it checks ----

    [HarmonyPatch(typeof(Il2Cpp.PlayerMovement), "DeadCheck")]
    static class SplitsPatch
    {
        static void Prefix(Il2Cpp.PlayerMovement __instance, out float __state)
        {
            __state = __instance.legLength;
            if (!Splits.IsNormal) __instance.legLength = __state * Splits.Value;
        }

        static void Postfix(Il2Cpp.PlayerMovement __instance, float __state)
        {
            if (!Splits.IsNormal) __instance.legLength = __state;
        }
    }

    // ---- limits, gravity, fatigue, wind: around nate's physics step ----

    static IntPtr tunedFor;
    static float baseDeadAng, baseInstaDeadAng, baseOneLegDeadAng, baseHipCollidingDeadAng, baseOffBalanceTime, baseDeathTime, baseHipCollision;
    static Vector3 baseWorldGravity; static bool haveWorldGravity; static float appliedWorldGravity = 1f;
    static readonly float[] fatigueBefore = new float[2];

    [HarmonyPatch(typeof(Il2Cpp.PlayerMovement), "FixedUpdate")]
    static class StepPatch
    {
        static void Prefix(Il2Cpp.PlayerMovement __instance)
        {
            try
            {
                var pm = __instance;
                if (pm.Pointer != tunedFor) Capture(pm);
                Tune(pm);
                BodyStep(pm);
                var f = pm.feet;
                for (int i = 0; i < 2 && f != null && i < f.Length; i++) fatigueBefore[i] = f[i] != null ? f[i].fatigue : 0f;
            }
            catch (Exception e) { Once("step", e); }
        }

        static void Postfix(Il2Cpp.PlayerMovement __instance)
        {
            try
            {
                var pm = __instance;
                // fatigue: scale only what this step added
                if (!Fatigue.IsNormal)
                {
                    var f = pm.feet;
                    for (int i = 0; i < 2 && f != null && i < f.Length; i++)
                    {
                        if (f[i] == null) continue;
                        float added = f[i].fatigue - fatigueBefore[i];
                        if (added > 0f) f[i].fatigue = fatigueBefore[i] + added * Fatigue.Value;
                    }
                }
                if (Wind.Value > 0f && !pm.dead) pm.hipVel += WindNow() * Time.fixedDeltaTime;
            }
            catch (Exception e) { Once("step after", e); }
        }
    }

    // nate's own values, read once per nate so scaling never compounds
    static void Capture(Il2Cpp.PlayerMovement pm)
    {
        tunedFor = pm.Pointer;
        baseDeadAng = pm.deadAngThresh; baseInstaDeadAng = pm.instaDeadAngThresh; baseOneLegDeadAng = pm.oneLegDeadAngThresh;
        baseHipCollidingDeadAng = pm.hipCollidingDeadAngThresh; baseOffBalanceTime = pm.offBalanceDeathTime;
        baseDeathTime = pm.deathTime; baseHipCollision = pm.hipCollisionDeathThresh;
        DebugLog.Write($"nate's values: lean {baseDeadAng}/{baseInstaDeadAng}/{baseOneLegDeadAng} deg, off balance {baseOffBalanceTime} s, squash {baseDeathTime} s, hip hit {baseHipCollision}");
    }

    static void Tune(Il2Cpp.PlayerMovement pm)
    {
        float k = Lean.Value;
        pm.deadAngThresh = Ang(baseDeadAng, k);
        pm.instaDeadAngThresh = Ang(baseInstaDeadAng, k);
        pm.oneLegDeadAngThresh = Ang(baseOneLegDeadAng, k);
        pm.hipCollidingDeadAngThresh = Ang(baseHipCollidingDeadAng, k);
        pm.offBalanceDeathTime = baseOffBalanceTime * Balance.Value;
        pm.deathTime = baseDeathTime * Balance.Value;
        pm.hipCollisionDeathThresh = baseHipCollision * Landings.Value;

        // the world's gravity (ragdoll, items) follows, touched only when the dial moves
        if (!Mathf.Approximately(appliedWorldGravity, Gravity.Value))
        {
            if (!haveWorldGravity) { baseWorldGravity = Physics.gravity; haveWorldGravity = true; }
            Physics.gravity = baseWorldGravity * Gravity.Value;
            appliedWorldGravity = Gravity.Value;
        }
    }

    // nate's own gravity: the game works it out every step (9.8, less in water) inside GetHipGravityAccel and keeps it in
    // pm.grav, which his hip, lifted feet, push-offs and leg torque all use. scaling it here reaches all of them
    // (setting pm.grav before the step did nothing: this overwrote it, so only the ragdoll felt the dial)
    [HarmonyPatch(typeof(Il2Cpp.PlayerMovement), "GetHipGravityAccel")]
    static class GravityPatch
    {
        static void Postfix(Il2Cpp.PlayerMovement __instance, ref Vector3 __result)
        {
            if (Gravity.IsNormal) return;
            float g = Gravity.Value;
            __instance.grav *= g;
            __result *= g;
        }
    }

    // ---- misc: keep items and hat ----
    // the game drops what's in a hand when the hand hits something hard (BodyPartHit) or flails while nate is down
    // (DeadUpdate), and knocks the hat off when his head jolts while down (DeadUpdate). the same drop / knock-off calls
    // also swap items and hats on purpose, so only these two callers are blocked

    static int inHit, inDown;

    [HarmonyPatch(typeof(Il2Cpp.PlayerMovement), "BodyPartHit")]
    static class HitPatch
    {
        static void Prefix() => inHit++;
        static void Finalizer() => inHit--;
    }

    [HarmonyPatch(typeof(Il2Cpp.PlayerMovement), "DeadUpdate")]
    static class DownPatch
    {
        static void Prefix() => inDown++;
        static void Finalizer() => inDown--;
    }

    [HarmonyPatch(typeof(Il2Cpp.PlayerMovement), nameof(Il2Cpp.PlayerMovement.DropHandItem))]
    static class DropPatch
    {
        static bool Prefix() => !(KeepItems.Value && (inHit > 0 || inDown > 0));
    }

    [HarmonyPatch(typeof(Il2Cpp.PlayerMovement), nameof(Il2Cpp.PlayerMovement.KnockOffHat))]
    static class HatPatch
    {
        static bool Prefix() => !(KeepHat.Value && inDown > 0);
    }

    // angles grow toward lying flat, never past it
    static float Ang(float a, float k) => Mathf.Min(a * k, 85f);

    // gusts: strength wobbles, direction drifts slowly
    static Vector3 WindNow()
    {
        float t = Time.time;
        float dir = Mathf.PerlinNoise(t * 0.03f, 0.5f) * 720f * Mathf.Deg2Rad;
        float gust = 0.35f + 0.65f * Mathf.PerlinNoise(0.5f, t * 0.4f);
        return new Vector3(Mathf.Cos(dir), 0f, Mathf.Sin(dir)) * (Wind.Value * 0.15f * gust);
    }

    // ---- body sizes ----
    // nate has three skeletons with the same bone names: the ragdoll under PuppetMaster (rigidbodies, colliders,
    // joints), the visible one the mesh follows, and a hidden posed copy the game paints mud on. each dial scales its
    // bone in all three; bones under it keep their own size (their local scale is divided by what's above), so a
    // longer torso pushes the head out without growing it. thighs and shins also set the leg length the walking uses (thigh + shin, 0.425 + 0.364 m),
    // feet the sole size it balances on

    static readonly (string name, Func<Dial> dial)[] BodyBones =
    {
        ("root.x", () => Hips), ("spine_01.x", () => Torso), ("head.x", () => Head),
        ("arm_stretch.l", () => UpperArms), ("arm_stretch.r", () => UpperArms),
        ("forearm_stretch.l", () => Forearms), ("forearm_stretch.r", () => Forearms),
        ("hand.l", () => Hands), ("hand.r", () => Hands),
        ("thigh_stretch.l", () => Thighs), ("thigh_stretch.r", () => Thighs),
        ("leg_stretch.l", () => Shins), ("leg_stretch.r", () => Shins),
        ("foot.l", () => Feet), ("foot.r", () => Feet),
    };

    static bool BodyNormal => Whole.IsNormal && Hips.IsNormal && Head.IsNormal && UpperArms.IsNormal && Forearms.IsNormal && Hands.IsNormal && Torso.IsNormal && Thighs.IsNormal && Shins.IsNormal && Feet.IsNormal;

    class Rig { public List<(Transform t, Dial d)> bones = new(); }
    static IntPtr bodyFor;
    static Rig ragdoll;
    static readonly List<Rig> looks = new();   // the visible skeleton, and the hidden posed copy the game paints mud on
    static float baseLegLength, baseThigh, baseShin; static Vector3 baseFootSize;

    // the walking's own size settings, scaled with the legs (the camera with all of nate). the game also has
    // distances written into its code (a lifted leg never under 0.57 m, 5 cm balance margins, its hip height...), so
    // a bigger nate stands on over-stretched legs and loses the ground and a smaller one squats. i measured him
    // walking at each size: he plays from about x0.5 to x1.15
    const float MinLeg = 0.5f, MaxLeg = 1.15f;
    // a bigger nate under the same gravity: lengths x size, speeds x sqrt(size), spring / turning strengths (they
    // turn metres or degrees of error into acceleration) x 1/size, so he moves like a scaled nate instead of
    // bouncing (x2 with plain-scaled springs threw his hip up off the ground)
    static readonly string[] LegLengths = { "bonusLegLength", "bonusLegLengthOneFoot", "sphereOffset", "maxLegHeight", "maxLegHeightBehind",
        "footMinYToHip", "maxFootMovingDistFromHip", "footDontCrossDist", "balanceHipWidth", "maxTargetDist",
        "dontSquishLength", "footTooHighDistToHip" };
    static readonly string[] LegSpeeds = { "footSpeed", "footPlantSpeed", "curStickTargetVel" };
    static readonly string[] LegGains = { "twoFootLegP", "oneFootLegP", "legTorqueP" };   // legP is picked from the first two each step
    static readonly string[] CamLengths = { "camCloseDist", "camExtraCloseDist", "camFarDist" };
    static readonly Dictionary<string, float> baseTuning = new();
    static float legBoost = 1f;
    static float curLeg = 1f;      // the leg scale in use this step

    // the game calls a foot grounded when it's within a fixed 7.5 cm (3.5 cm while lifting) of the ground, a number
    // in its code. a big nate's foot sits further off than that even standing, so his feet kept "leaving" the ground.
    // the distance the game measures is divided by nate's size, so those limits grow and shrink with him
    [HarmonyPatch]
    static class GroundDistPatch
    {
        static IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            foreach (var m in typeof(Il2Cpp.PlayerMovement).GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
                if (m.Name == "TargetDistFromGround" && m.ReturnType == typeof(float)) yield return m;
        }

        static void Postfix(ref float __result)
        {
            if (curLeg > 0.01f && Mathf.Abs(curLeg - 1f) > 0.001f) __result /= curLeg;
        }
    }

    // thighs and shins raised together so the whole leg stays between MinLeg and MaxLeg
    static float LegScale()
    {
        float raw = Whole.Value * (baseThigh * Thighs.Value + baseShin * Shins.Value) / (baseThigh + baseShin);
        legBoost = raw < MinLeg ? MinLeg / raw : raw > MaxLeg ? MaxLeg / raw : 1f;
        return raw * legBoost;
    }

    static readonly Dictionary<string, System.Reflection.PropertyInfo> props = new();
    static System.Reflection.PropertyInfo Prop(string name)
    {
        if (!props.TryGetValue(name, out var p)) props[name] = p = typeof(Il2Cpp.PlayerMovement).GetProperty(name);
        return p;
    }
    static float Get(Il2Cpp.PlayerMovement pm, string name) => (float)Prop(name).GetValue(pm);
    static void Set(Il2Cpp.PlayerMovement pm, string name, float v) => Prop(name).SetValue(pm, v);
    static readonly float[] appliedRagdoll = new float[10];
    static bool ragdollApplied;
    static bool bodyTouched, visibleTouched;

    static void FindRigs(Il2Cpp.PlayerMovement pm)
    {
        bodyFor = pm.Pointer;
        ragdoll = null; looks.Clear(); ragdollApplied = false;
        var root = pm.transform.root;
        Transform ragRoot = null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name != "root.x" || t.parent == null) continue;
            if (t.parent.GetComponent<Il2CppRootMotion.Dynamics.PuppetMaster>() != null) ragRoot = t;
            else { var r = Collect(t); if (r != null && r.bones.Count > 0) looks.Add(r); }
        }
        ragdoll = Collect(ragRoot);
        baseLegLength = pm.legLength; baseFootSize = pm.footSize;
        baseTuning.Clear();
        foreach (var n in LegLengths) baseTuning[n] = Get(pm, n);
        foreach (var n in LegSpeeds) baseTuning[n] = Get(pm, n);
        foreach (var n in LegGains) baseTuning[n] = Get(pm, n);
        foreach (var n in CamLengths) baseTuning[n] = Get(pm, n);
        baseTuning["maxFootTorque"] = pm.maxFootTorque;
        // thigh / shin lengths from the ragdoll's own offsets (shin root under the thigh, foot under the shin)
        baseThigh = 0.425f; baseShin = 0.364f;
        if (ragRoot != null)
        {
            var shin = Find(ragRoot, "leg_stretch.l"); var foot = Find(ragRoot, "foot.l");
            if (shin != null && foot != null) { baseThigh = shin.localPosition.magnitude; baseShin = foot.localPosition.magnitude; }
        }
        DebugLog.Write($"body: ragdoll {ragdoll?.bones.Count ?? 0} bones, {looks.Count} other skeletons ({string.Join(", ", looks.ConvertAll(r => r.bones.Count + " bones"))}), leg {baseLegLength:F3} m (thigh {baseThigh:F3} + shin {baseShin:F3})");
    }

    // the bones we scale, parents first
    static Rig Collect(Transform rigRoot)
    {
        if (rigRoot == null) return null;
        var rig = new Rig();
        foreach (var t in rigRoot.GetComponentsInChildren<Transform>(true))   // depth-first: parents before children
            foreach (var (name, dial) in BodyBones)
                if (t.name == name) { rig.bones.Add((t, dial())); break; }
        return rig;
    }

    // each bone ends up at its dial's size whatever the bones above it are at
    static readonly Dictionary<IntPtr, float> rigWant = new();

    static void ApplyRig(Rig rig)
    {
        if (rig == null) return;
        var want = rigWant;
        want.Clear();
        foreach (var (t, d) in rig.bones)
        {
            float above = 1f;
            for (var p = t.parent; p != null; p = p.parent)
                if (want.TryGetValue(p.Pointer, out var k)) { above = k; break; }
            float k2 = d.Value * Whole.Value * ((d == Thighs || d == Shins) ? legBoost : 1f);
            want[t.Pointer] = k2;
            float local = k2 / above;
            t.localScale = new Vector3(local, local, local);
        }
    }

    // ---- multiplayer: our bone sizes for other players, theirs for our copies of their nates (RulebookNet) ----

    internal const int BoneGroups = 9;
    static readonly Dictionary<string, int> groupOf = new()
    {
        ["root.x"] = 0, ["spine_01.x"] = 1, ["head.x"] = 2,
        ["arm_stretch.l"] = 3, ["arm_stretch.r"] = 3, ["forearm_stretch.l"] = 4, ["forearm_stretch.r"] = 4,
        ["hand.l"] = 5, ["hand.r"] = 5, ["thigh_stretch.l"] = 6, ["thigh_stretch.r"] = 6,
        ["leg_stretch.l"] = 7, ["leg_stretch.r"] = 7, ["foot.l"] = 8, ["foot.r"] = 8,
    };

    // each group's size, as our own bones get it
    internal static float[] BoneScales()
    {
        LegScale();
        float w = Whole.Value;
        return new[] { Hips.Value * w, Torso.Value * w, Head.Value * w, UpperArms.Value * w, Forearms.Value * w, Hands.Value * w,
            Thighs.Value * w * legBoost, Shins.Value * w * legBoost, Feet.Value * w };
    }

    // a remote nate's bones at those sizes (null = normal), bones under a sized one keeping their own size
    internal static void ScaleBones(List<Transform> bones, float[] scales)
    {
        var list = new List<(Transform t, int depth, float want)>();
        foreach (var t in bones)
        {
            if (t == null || !groupOf.TryGetValue(t.name, out int g)) continue;
            int depth = 0; for (var p = t.parent; p != null; p = p.parent) depth++;
            list.Add((t, depth, scales == null ? 1f : scales[g]));
        }
        list.Sort((a, b) => a.depth.CompareTo(b.depth));
        var want = new Dictionary<IntPtr, float>();
        foreach (var (t, _, k) in list)
        {
            float above = 1f;
            for (var p = t.parent; p != null; p = p.parent)
                if (want.TryGetValue(p.Pointer, out var a)) { above = a; break; }
            want[t.Pointer] = k;
            float local = k / above;
            t.localScale = new Vector3(local, local, local);
        }
    }

    static Dial[] bodyDials;   // made on first use: the dials don't exist before OnInitializeMelon

    // true when a body dial moved since the ragdoll was last sized
    static bool BodyMoved()
    {
        bodyDials ??= new[] { Whole, Hips, Head, UpperArms, Forearms, Hands, Torso, Thighs, Shins, Feet };
        bool moved = !ragdollApplied;
        for (int i = 0; i < bodyDials.Length; i++)
        {
            float v = bodyDials[i].Value;
            if (v != appliedRagdoll[i]) { appliedRagdoll[i] = v; moved = true; }
        }
        ragdollApplied = true;
        return moved;
    }

    // physics side, before nate's step: ragdoll bones (only when a dial moved), leg length, sole size
    static void BodyStep(Il2Cpp.PlayerMovement pm)
    {
        bool normal = BodyNormal;
        if (normal && !bodyTouched) { curLeg = 1f; return; }
        if (pm.Pointer != bodyFor) FindRigs(pm);
        float leg = LegScale();
        curLeg = leg;
        if (BodyMoved()) ApplyRig(ragdoll);
        pm.legLength = baseLegLength * leg;
        pm.footSize = baseFootSize * Mathf.Max(Feet.Value * Whole.Value, MinLeg);
        foreach (var n in LegLengths) Set(pm, n, baseTuning[n] * leg);
        foreach (var n in LegSpeeds) Set(pm, n, baseTuning[n] * Mathf.Sqrt(leg));
        foreach (var n in LegGains) Set(pm, n, baseTuning[n] / leg);
        foreach (var n in CamLengths) Set(pm, n, baseTuning[n] * Whole.Value);
        pm.maxFootTorque = baseTuning["maxFootTorque"] * leg * leg;
        bodyTouched = !normal;
    }

    public override void OnLateUpdate()
    {
        if (menuOk) try { HubLateUpdate(); } catch (Exception e) { Once("menu", e); }
        // looks side, after animation: the visible bones
        try
        {
            bool normal = BodyNormal;
            if (looks.Count == 0 || (normal && !visibleTouched)) return;
            foreach (var r in looks) ApplyRig(r);
            visibleTouched = !normal;
        }
        catch (Exception e) { Once("body", e); looks.Clear(); }
    }

    static Transform Find(Transform under, string name)
    {
        foreach (var t in under.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
        return null;
    }

    // ---- suit colour ----
    // the same as the multiplayer mod's: the suit material's colour, set straight. that mod only colours nate while
    // it's connected (and puts white back when it leaves), so while online this keeps its hands off; offline it's ours

    internal static Color SuitColor() => new(Red.Value, Green.Value, Blue.Value, 1f);

    static IntPtr suitFor;
    static readonly List<(Material mat, Color was)> suits = new();
    static bool suitTouched;

    static void Suit()
    {
        var pm = Il2Cpp.PlayerMovement.me;
        if (pm == null) return;
        bool normal = Red.IsNormal && Green.IsNormal && Blue.IsNormal;
        if (Online()) { suitTouched = false; return; }
        if (normal && !suitTouched) return;
        if (pm.Pointer != suitFor) FindSuits(pm);
        var want = SuitColor();
        foreach (var (mat, was) in suits)
        {
            if (mat == null) continue;
            var c = normal ? was : want;
            if (mat.color != c) mat.color = c;
        }
        suitTouched = !normal;
    }

    // the walking code's suit material, and the one on nate's mesh (the multiplayer mod sets both)
    static void FindSuits(Il2Cpp.PlayerMovement pm)
    {
        suitFor = pm.Pointer;
        suits.Clear();
        void Keep(Material m) { if (m != null && !suits.Exists(x => x.mat.Pointer == m.Pointer)) suits.Add((m, m.color)); }
        Keep(pm.suitMat);
        var mesh = pm.transform.Find("IKTargets/HipTarget/NathanAnimIK_October2022/Nathan.001")?.GetComponent<SkinnedMeshRenderer>();
        if (mesh != null)
            foreach (var m in mesh.sharedMaterials)
                if (m != null && m.name.Contains("NewSuit_Oct22")) Keep(m);
        DebugLog.Write($"suit colour: {suits.Count} suit material{(suits.Count == 1 ? "" : "s")}");
    }

    static bool Online()
    {
        if (!netOk) return false;
        try { return NetOnline(); }
        catch (Exception e) when (e is FileNotFoundException || e is TypeLoadException || e is MissingMethodException || e is MissingFieldException) { netOk = false; return false; }
    }

    static bool NetOnline() => RulebookNet.Online();

    // ---- the tag ----
    // the rules that aren't normal, listed down the left side of the screen: under PracticeTools' timer and splits
    // when they show, above the game's input display and WalkingCoach's panel when they show. one column, two when that doesn't fit, then
    // smaller text until it does

    TextMeshProUGUI[] cols;
    RectTransform canvasRt;
    readonly List<string> lines = new();
    readonly System.Text.StringBuilder sb = new();
    string laidOut;
    float top = 20f, bottom = 30f, nextLimits;
    const float BaseSize = 22f, MinSize = 10f, Gap = 16f, Left = 30f, ColGap = 28f;

    public override void OnUpdate()
    {
        try { Tag(); } catch (Exception e) { Once("tag", e); }
        try { Suit(); } catch (Exception e) { Once("suit colour", e); }
        if (menuOk) try { MenuHighlight(); } catch (Exception e) { menuOk = false; LoggerInstance.Error("menu: " + e); }
        if (netOk) try { NetUpdate(); }
            catch (Exception e) when (e is FileNotFoundException || e is TypeLoadException || e is MissingMethodException || e is MissingFieldException) { netOk = false; }
            catch (Exception e) { netOk = false; LoggerInstance.Error("multiplayer sizes: " + e); }
    }

    static bool menuOk;   // the RULEBOOK page exists (BabyStepsMenuLib installed)
    static bool netOk = true;   // the multiplayer mod is there (switched off on the first sign it isn't)
    static void NetUpdate() => RulebookNet.Update();

    // separate method so a missing BabyStepsMenuLib only fails here
    static void MenuHighlight() => RulebookMenu.Update();

    void Tag()
    {
        var m = Il2Cpp.Menu.me;
        bool changed = false;
        foreach (var d in Dials) if (!d.IsNormal && !d.Look) { changed = true; break; }
        if (KeepItems.Value || KeepHat.Value) changed = true;
        bool show = ShowTag.Value && changed && Il2Cpp.PlayerMovement.me != null && m != null;
        if (!show) { if (cols != null) foreach (var c in cols) c.gameObject.SetActive(false); return; }
        if (cols == null && !MakeTag(m)) return;

        lines.Clear();
        lines.Add("RULEBOOK");
        foreach (var d in Dials) if (!d.IsNormal && !d.Look) lines.Add(d.Tag + " " + d.Show(d.Value));
        if (KeepItems.Value) lines.Add("KEEP ITEMS");
        if (KeepHat.Value) lines.Add("KEEP HAT");
        if (Time.unscaledTime >= nextLimits) { nextLimits = Time.unscaledTime + 0.25f; Limits(); }

        // laid out again only when the rules or the room changed
        sb.Clear();
        foreach (var l in lines) sb.Append(l).Append('\n');
        sb.Append((int)top).Append('|').Append((int)bottom).Append('|').Append((int)canvasRt.rect.height);
        string key = sb.ToString();
        cols[0].gameObject.SetActive(true);
        if (key == laidOut) return;
        laidOut = key;
        Layout(Mathf.Max(60f, canvasRt.rect.height - top - bottom));
    }

    void Layout(float room)
    {
        float size = BaseSize;
        string a = string.Join("\n", lines), b = null;
        float h = Height(cols[0], a, size);
        if (h > room && lines.Count > 1)
        {
            int half = (lines.Count + 1) / 2;
            a = string.Join("\n", lines.GetRange(0, half));
            b = string.Join("\n", lines.GetRange(half, lines.Count - half));
            h = Height(cols[0], a, size);
        }
        if (h > room) size = Mathf.Max(MinSize, size * room / h);
        Place(cols[0], a, size, Left);
        cols[1].gameObject.SetActive(b != null);
        if (b != null) Place(cols[1], b, size, Left + cols[0].GetPreferredValues(a).x + ColGap * size / BaseSize);
    }

    static float Height(TMP_Text t, string s, float size)
    {
        t.fontSize = size;
        return t.GetPreferredValues(s).y;
    }

    void Place(TextMeshProUGUI t, string s, float size, float x)
    {
        t.fontSize = size;
        if (t.text != s) t.text = s;
        t.rectTransform.anchoredPosition = new Vector2(x, -top);
    }

    // ---- the room on the left: canvas units from the top and from the bottom ----

    static readonly string[] HudTexts = { "timer", "steps", "splits" };
    Transform ptHud;
    float nextHudFind;
    static Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3> corners;

    void Limits()
    {
        float scale = canvasRt.localScale.y > 0f ? canvasRt.localScale.y : 1f;
        top = 20f;
        if (ptHud == null && Time.unscaledTime >= nextHudFind)
        {
            nextHudFind = Time.unscaledTime + 2f;
            ptHud = GameObject.Find("PracticeTools hud")?.transform;
        }
        if (ptHud != null)
            foreach (var n in HudTexts)
            {
                var c = ptHud.Find(n);
                if (c == null || !c.gameObject.activeInHierarchy) continue;
                var t = c.GetComponent<TMP_Text>();
                if (t == null) continue;
                // its text runs down from its top left corner (its pivot), on an overlay canvas: screen pixels
                float bottomPx = c.position.y - t.preferredHeight * c.lossyScale.y;
                top = Mathf.Max(top, (Screen.height - bottomPx) / scale + Gap);
            }

        bottom = 30f;
        var cam = Camera.main;
        var g = cam != null ? cam.GetComponent<Il2Cpp.InputGraph>() : null;
        var img = g != null && g.enabled ? g.rtImage : null;
        if (img != null && img.gameObject.activeInHierarchy)
        {
            corners ??= new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(4);
            img.rectTransform.GetWorldCorners(corners);
            var cv = img.canvas;
            Camera uiCam = cv != null && cv.renderMode != RenderMode.ScreenSpaceOverlay ? cv.worldCamera : null;
            float topPx = 0f;
            for (int i = 0; i < 4; i++) topPx = Mathf.Max(topPx, RectTransformUtility.WorldToScreenPoint(uiCam, corners[i]).y);
            bottom = Mathf.Max(bottom, topPx / scale + Gap);
        }

        // WalkingCoach's panel: text running up from its bottom left corner (its pivot)
        if (coach == null && Time.unscaledTime >= nextCoachFind)
        {
            nextCoachFind = Time.unscaledTime + 2f;
            coach = GameObject.Find("WalkingCoach")?.transform.Find("text");
        }
        if (coach != null && coach.gameObject.activeInHierarchy)
        {
            var t = coach.GetComponent<TMP_Text>();
            if (t != null && t.text.Length > 0)
                bottom = Mathf.Max(bottom, (coach.position.y + t.preferredHeight * coach.lossyScale.y) / scale + Gap);
        }
    }

    Transform coach;
    float nextCoachFind;

    bool MakeTag(Il2Cpp.Menu m)
    {
        TMP_Text src = m.ContinueGameButtonText;
        var go = new GameObject("Rulebook tag");
        UnityEngine.Object.DontDestroyOnLoad(go);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 450;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 1f;
        canvasRt = go.GetComponent<RectTransform>();
        cols = new TextMeshProUGUI[2];
        for (int i = 0; i < 2; i++)
        {
            var t = new GameObject("column " + (i + 1));
            t.transform.SetParent(go.transform, false);
            var c = cols[i] = t.AddComponent<TextMeshProUGUI>();
            if (src != null) { c.font = src.font; c.fontSharedMaterial = src.fontSharedMaterial; }
            c.fontSize = BaseSize;
            c.color = new Color(1f, 0.85f, 0.4f, 0.8f);
            c.alignment = TextAlignmentOptions.TopLeft;
            c.enableWordWrapping = false;
            c.raycastTarget = false;
            var rt = c.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(700f, 1000f);
            t.SetActive(false);
        }
        laidOut = null;
        return true;
    }
}
