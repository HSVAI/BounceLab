#if UNITY_STANDALONE_LINUX
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace BounceLab
{
    public sealed class SmokeCheck : MonoBehaviour
    {
        private BounceLabGame game;
        private int errors;
        private const string Output = "/home/ghtnql/BounceLab/Builds/QA";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Attach()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--bouncelab-smoke") >= 0)
                new GameObject("BounceLab Smoke Check").AddComponent<SmokeCheck>();
        }

        private void OnEnable() { Application.logMessageReceived += OnLog; }
        private void OnDisable() { Application.logMessageReceived -= OnLog; }
        private void OnLog(string message, string trace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
        }

        private IEnumerator Start()
        {
            Directory.CreateDirectory(Output);
            yield return new WaitForSeconds(1);
            game = FindObjectOfType<BounceLabGame>();
            Check(game != null, "Game exists");
            Check(Get("mode").ToString() == "Home", "Home screen");
            yield return Screenshot("01-home.png");

            Call("StartPlay", MapRules.Training(), false);
            yield return new WaitForSeconds(.4f);
            Check(Get("mode").ToString() == "Play", "Training starts");
            Check(Get<float>("elapsed") > 0, "Timer advances");
            float previousX = Get<float>("ballX");
            Set("control", 1);
            yield return new WaitForSeconds(.35f);
            Set("control", 0);
            Check(Get<float>("ballX") > previousX, "Hold right steers ball");
            yield return Screenshot("02-play.png");

            Call("Die");
            Check(Get<int>("deaths") == 1, "Death counter");
            yield return new WaitForSeconds(.8f);
            Check(Get<float>("deathTimer") <= 0, "Automatic respawn");

            Call("ShowEditor");
            yield return new WaitForSeconds(.1f);
            Check(Get("mode").ToString() == "Editor", "Editor opens");
            Check(Get<UnityEngine.UI.Image[]>("editorCells").Length == 160, "Editor has 160 cells");
            var draft = Get<MapData>("draft");
            Check(MapRules.Validate(draft) == "", "Draft validates");
            Set("brush", MapRules.Spike);
            Call("Paint", MapRules.Index(4, 4));
            Check(draft.tiles[MapRules.Index(4, 4)] == MapRules.Spike, "Tile painting");
            yield return Screenshot("03-editor.png");

            Call("TestDraft");
            yield return new WaitForSeconds(.2f);
            Check(Get("mode").ToString() == "Play", "Editor test play");
            Check(Get<bool>("returnToEditor"), "Test returns to editor");
            Call("Win");
            Check(Get<bool>("won"), "Goal completes level");
            yield return Screenshot("04-clear.png");
            Check(errors == 0, "No player errors");
            Debug.Log("BOUNCELAB_PLAYER_SMOKE_PASS");
            Application.Quit(0);
        }

        private IEnumerator Screenshot(string name)
        {
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(Output, name);
            ScreenCapture.CaptureScreenshot(path);
            yield return new WaitForSeconds(.25f);
            Check(File.Exists(path), "Screenshot " + name);
        }

        private object Get(string name) { return Field(name).GetValue(game); }
        private T Get<T>(string name) { return (T)Get(name); }
        private void Set(string name, object value) { Field(name).SetValue(game, value); }
        private FieldInfo Field(string name) { return typeof(BounceLabGame).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic); }
        private void Call(string name, params object[] args)
        { typeof(BounceLabGame).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(game, args); }

        private static void Check(bool ok, string message)
        {
            if (ok) { Debug.Log("SMOKE_OK " + message); return; }
            Debug.LogError("SMOKE_FAILED " + message);
            Application.Quit(1);
            throw new Exception(message);
        }
    }
}
#endif
