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
            var audio = FindObjectOfType<BounceAudio>();
            Check(audio != null && audio.MusicSampleCount > 300000, "Original music loop generated");
            bool musicSetting = audio.MusicEnabled;
            audio.ToggleMusic();
            audio.ToggleMusic();
            Check(audio.MusicEnabled == musicSetting, "Music toggle restores setting");
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

            float deadline = Time.time + 24f;
            float nextTrace = Time.time;
            int routeStage = 0;
            while (!Get<bool>("won") && Time.time < deadline)
            {
                float y = Get<float>("ballY");
                if (routeStage == 0 && y > 2.35f) routeStage++;
                else if (routeStage == 1 && y > 4.35f) routeStage++;
                else if (routeStage == 2 && y > 6.35f) routeStage++;
                else if (routeStage == 3 && y > 8.35f) routeStage++;
                else if (routeStage == 4 && y > 10.35f) routeStage++;
                else if (routeStage == 5 && y > 12.35f) routeStage++;
                float[] route = { 4.5f, 7.3f, 5.5f, 7.3f, 5.5f, 7.3f, 5.4f };
                float target = route[routeStage];
                float x = Get<float>("ballX");
                Set("control", Mathf.Abs(target - x) < .18f ? 0 : target > x ? 1 : -1);
                if (Time.time >= nextTrace)
                {
                    Debug.Log("SMOKE_ROUTE x=" + x.ToString("0.00") + " y=" + y.ToString("0.00") +
                        " vx=" + Get<float>("velocityX").ToString("0.00") + " vy=" + Get<float>("velocityY").ToString("0.00") +
                        " target=" + target.ToString("0.0") + " stage=" + routeStage + " falls=" + Get<int>("deaths"));
                    nextTrace += 1f;
                }
                yield return null;
            }
            Set("control", 0);
            Check(Get<bool>("won"), "Training map can be completed");
            yield return Screenshot("03-training-clear.png");

            Call("ShowEditor");
            yield return new WaitForSeconds(.1f);
            Check(Get("mode").ToString() == "Editor", "Editor opens");
            Check(Get<UnityEngine.UI.Image[]>("editorCells").Length == 160, "Editor has 160 cells");
            var draft = Get<MapData>("draft");
            Check(MapRules.Validate(draft) == "", "Draft validates");
            Set("brush", MapRules.Spike);
            Call("Paint", MapRules.Index(4, 4));
            Check(draft.tiles[MapRules.Index(4, 4)] == MapRules.Spike, "Tile painting");
            yield return Screenshot("04-editor.png");

            Call("TestDraft");
            yield return new WaitForSeconds(.2f);
            Check(Get("mode").ToString() == "Play", "Editor test play");
            Check(Get<bool>("returnToEditor"), "Test returns to editor");
            Call("Win");
            Check(Get<bool>("won"), "Goal completes level");
            yield return Screenshot("05-editor-clear.png");

            Call("ShowBrowse");
            float networkDeadline = Time.time + 15f;
            while (Get<UnityEngine.UI.Text>("statusText").text == "LOADING MAPS..." && Time.time < networkDeadline)
                yield return null;
            Check(Get<string>("apiBase").StartsWith("https://"), "GitHub Pages API discovery");
            Check(Get<UnityEngine.UI.Text>("statusText").text == "NEWEST MAPS", "Community maps load");
            yield return Screenshot("06-community.png");
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
