using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace BounceLab
{
    public sealed class BounceLabGame : MonoBehaviour
    {
        private const float Cell = 76f;
        private const float Radius = .33f;
        private const string ApiConfigUrl = "https://hsvai.github.io/BounceLab/api.json";
        private const string DraftKey = "BounceLabDraftV1";
        private enum Mode { Home, Play, Editor, Browse }

        private readonly Color background = new Color(.025f, .035f, .065f);
        private readonly Color panelColor = new Color(.055f, .075f, .12f);
        private readonly Color gridColor = new Color(.12f, .16f, .23f);
        private readonly Color cyan = new Color(.15f, 1f, .76f);
        private readonly Color coral = new Color(1f, .25f, .39f);
        private readonly Color yellow = new Color(1f, .82f, .24f);
        private readonly Color quiet = new Color(.48f, .56f, .68f);

        private RectTransform root, page, board, ball;
        private Font font;
        private BounceAudio sound;
        private Mode mode;
        private MapData currentMap, draft;
        private Image[] editorCells;
        private Text statusText, timerText, deathText, brushText;
        private InputField nameInput, authorInput;
        private int brush = MapRules.Block, control, deaths, pageVersion, draftRevision, clearedRevision = -1;
        private float ballX, ballY, velocityX, velocityY, elapsed, deathTimer;
        private bool won, returnToEditor;
        private string apiBase = "";

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            sound = GetComponent<BounceAudio>() ?? gameObject.AddComponent<BounceAudio>();
            sound.Initialize();
            font = Resources.Load<Font>("Unifont") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildRoot();
            LoadDraft();
            ShowHome();
            StartCoroutine(ResolveApi(null));
        }

        private void Update()
        {
            if (mode != Mode.Play || won) return;
            if (deathTimer > 0)
            {
                deathTimer -= Time.deltaTime;
                if (deathTimer <= 0) Respawn();
                return;
            }
            int keyboard = 0;
            if (UnityEngine.Input.GetKey(KeyCode.LeftArrow) || UnityEngine.Input.GetKey(KeyCode.A)) keyboard--;
            if (UnityEngine.Input.GetKey(KeyCode.RightArrow) || UnityEngine.Input.GetKey(KeyCode.D)) keyboard++;
            Simulate(Mathf.Min(Time.deltaTime, .033f), keyboard == 0 ? control : keyboard);
        }

        private void BuildRoot()
        {
            var canvasObject = new GameObject("Bounce Lab Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            root = canvasObject.GetComponent<RectTransform>();
            Stretch(Box("Background", root, Vector2.zero, Vector2.zero, background).rectTransform);
            if (FindObjectOfType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        private void NewPage(Mode next)
        {
            pageVersion++;
            control = 0;
            if (page != null) Destroy(page.gameObject);
            page = new GameObject(next + " Page", typeof(RectTransform)).GetComponent<RectTransform>();
            page.SetParent(root, false);
            Stretch(page);
            mode = next;
        }

        private void ShowHome()
        {
            NewPage(Mode.Home);
            Label("B O U N C E   L A B", 28, cyan, new Vector2(900, 60), new Vector2(0, 720), page);
            var title = Label("MAKE IT.\nBOUNCE IT.", 116, Color.white, new Vector2(940, 320), new Vector2(0, 450), page);
            title.fontStyle = FontStyle.Bold;
            Label("AUTO-BOUNCE PLATFORMING\nBUILT BY THE PLAYERS", 28, quiet, new Vector2(900, 120), new Vector2(0, 230), page);
            Button("PLAY TRAINING", new Vector2(760, 125), new Vector2(0, 20), cyan, page, () => StartPlay(MapRules.Training(), false));
            Button("COMMUNITY MAPS", new Vector2(760, 125), new Vector2(0, -145), yellow, page, ShowBrowse);
            Button("MAP MAKER", new Vector2(760, 125), new Vector2(0, -310), coral, page, ShowEditor);
            Label("HOLD LEFT / RIGHT · THE BALL BOUNCES BY ITSELF", 22, quiet, new Vector2(960, 60), new Vector2(0, -500), page);
            Button(sound.MusicEnabled ? "BGM ON" : "BGM OFF", new Vector2(330, 78), new Vector2(-185, -590), panelColor, page,
                () => { sound.ToggleMusic(); ShowHome(); });
            Button(sound.SfxEnabled ? "SFX ON" : "SFX OFF", new Vector2(330, 78), new Vector2(185, -590), panelColor, page,
                () => { sound.ToggleSfx(); ShowHome(); });
            statusText = Label(apiBase.Length > 0 ? "SERVER ONLINE" : "CONNECTING TO MAP SERVER...", 21,
                apiBase.Length > 0 ? cyan : quiet, new Vector2(850, 50), new Vector2(0, -710), page);
            Label("v0.3  ·  NEON REBOUND", 18, quiet, new Vector2(800, 40), new Vector2(0, -830), page);
        }

        private void StartPlay(MapData map, bool editorReturn)
        {
            string error = MapRules.Validate(map);
            if (error.Length > 0)
            {
                if (statusText != null) statusText.text = error;
                return;
            }
            currentMap = map.Clone();
            returnToEditor = editorReturn;
            NewPage(Mode.Play);
            Label(currentMap.name.ToUpperInvariant(), 30, Color.white, new Vector2(750, 60), new Vector2(0, 855), page).fontStyle = FontStyle.Bold;
            Label("BY " + currentMap.author.ToUpperInvariant(), 20, quiet, new Vector2(600, 45), new Vector2(0, 808), page);
            timerText = Label("00.00", 40, cyan, new Vector2(280, 60), new Vector2(-315, 755), page);
            deathText = Label("FALLS 0", 24, quiet, new Vector2(280, 60), new Vector2(315, 755), page);
            Button("×", new Vector2(90, 90), new Vector2(450, 830), quiet, page,
                () => { if (returnToEditor) ShowEditor(); else ShowHome(); });
            BuildPlayBoard();
            var left = Button("◀", new Vector2(420, 150), new Vector2(-235, -810), panelColor, page, null);
            var right = Button("▶", new Vector2(420, 150), new Vector2(235, -810), panelColor, page, null);
            Hold(left, -1); Hold(right, 1);
            Label("HOLD TO STEER", 20, quiet, new Vector2(700, 45), new Vector2(0, -700), page);
            deaths = 0;
            elapsed = 0;
            won = false;
            Respawn();
            Debug.Log("BOUNCELAB_PLAY_STARTED map=" + currentMap.name);
        }

        private void BuildPlayBoard()
        {
            board = Panel("Level", page, new Vector2(Cell * MapRules.Width, Cell * MapRules.Height), new Vector2(0, 70), new Color(.035f, .05f, .08f));
            for (int y = 0; y < MapRules.Height; y++)
            for (int x = 0; x < MapRules.Width; x++)
            {
                int tile = currentMap.tiles[MapRules.Index(x, y)];
                if (tile == MapRules.Empty || tile == MapRules.Spawn) continue;
                DrawTile(tile, x, y, board, Cell - 3, false);
            }
            ball = Box("Ball", board, new Vector2(Cell * .66f, Cell * .66f), Vector2.zero, cyan).rectTransform;
            Box("Ball Core", ball, new Vector2(Cell * .20f, Cell * .20f), Vector2.zero, background);
        }

        private void Respawn()
        {
            for (int i = 0; i < currentMap.tiles.Length; i++)
            {
                if (currentMap.tiles[i] != MapRules.Spawn) continue;
                ballX = i % MapRules.Width + .5f;
                ballY = i / MapRules.Width + .5f;
                break;
            }
            velocityX = 0;
            velocityY = 8.8f;
            ball.gameObject.SetActive(true);
            ball.GetComponent<Image>().color = cyan;
            PositionBall();
        }

        private void Simulate(float dt, int direction)
        {
            elapsed += dt;
            timerText.text = elapsed.ToString("00.00");
            velocityX = Mathf.MoveTowards(velocityX, Mathf.Clamp(direction, -1, 1) * 4.7f, 16f * dt);
            velocityY -= 19f * dt;

            float nextX = ballX + velocityX * dt;
            if (velocityX > 0)
            {
                int wallX = Mathf.FloorToInt(nextX + Radius);
                if (SolidAt(wallX, Mathf.FloorToInt(ballY - Radius + .04f)) || SolidAt(wallX, Mathf.FloorToInt(ballY + Radius - .04f)))
                { nextX = wallX - Radius; velocityX = 0; }
            }
            else if (velocityX < 0)
            {
                int wallX = Mathf.FloorToInt(nextX - Radius);
                if (SolidAt(wallX, Mathf.FloorToInt(ballY - Radius + .04f)) || SolidAt(wallX, Mathf.FloorToInt(ballY + Radius - .04f)))
                { nextX = wallX + 1 + Radius; velocityX = 0; }
            }
            ballX = nextX;

            float nextY = ballY + velocityY * dt;
            if (velocityY <= 0)
            {
                int floorY = Mathf.FloorToInt(nextY - Radius);
                int leftX = Mathf.FloorToInt(ballX - Radius + .05f);
                int rightX = Mathf.FloorToInt(ballX + Radius - .05f);
                int leftTile = MapRules.Tile(currentMap, leftX, floorY);
                int rightTile = MapRules.Tile(currentMap, rightX, floorY);
                if (MapRules.IsSolid(leftTile) || MapRules.IsSolid(rightTile))
                {
                    float top = floorY + 1;
                    if (ballY - Radius >= top - .3f)
                    {
                        nextY = top + Radius;
                        bool spring = leftTile == MapRules.Spring || rightTile == MapRules.Spring;
                        velocityY = spring ? 13.2f : 9.6f;
                        sound.PlayBounce(spring);
                        if (velocityY > 12) Flash(ball.anchoredPosition, yellow);
                    }
                }
            }
            else
            {
                int ceilingY = Mathf.FloorToInt(nextY + Radius);
                if (SolidAt(Mathf.FloorToInt(ballX - Radius + .05f), ceilingY) ||
                    SolidAt(Mathf.FloorToInt(ballX + Radius - .05f), ceilingY))
                { nextY = ceilingY - Radius; velocityY = 0; }
            }
            ballY = nextY;
            PositionBall();
            CheckSpecialTiles();
            if (ballY < -.5f) Die();
        }

        private bool SolidAt(int x, int y)
        {
            if (y < 0) return false;
            if (x < 0 || x >= MapRules.Width || y >= MapRules.Height) return true;
            return MapRules.IsSolid(MapRules.Tile(currentMap, x, y));
        }

        private void CheckSpecialTiles()
        {
            int minX = Mathf.FloorToInt(ballX - Radius), maxX = Mathf.FloorToInt(ballX + Radius);
            int minY = Mathf.FloorToInt(ballY - Radius), maxY = Mathf.FloorToInt(ballY + Radius);
            for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                if (!MapRules.Inside(x, y)) continue;
                int tile = currentMap.tiles[MapRules.Index(x, y)];
                if (tile == MapRules.Spike) { Die(); return; }
                if (tile == MapRules.Goal) { Win(); return; }
            }
        }

        private void PositionBall()
        {
            ball.anchoredPosition = new Vector2((ballX - 5f) * Cell, (ballY - 8f) * Cell);
            ball.Rotate(0, 0, -velocityX * 55f * Time.deltaTime);
        }

        private void Die()
        {
            if (deathTimer > 0 || won) return;
            deaths++;
            deathText.text = "FALLS " + deaths;
            ball.GetComponent<Image>().color = coral;
            deathTimer = .6f;
            velocityX = velocityY = 0;
            sound.PlayDeath();
            Debug.Log("BOUNCELAB_RESPAWN falls=" + deaths);
        }

        private void Win()
        {
            if (won) return;
            won = true;
            control = 0;
            sound.PlayClear();
            ball.GetComponent<Image>().color = yellow;
            var modal = Panel("Clear", page, new Vector2(870, 650), new Vector2(0, 30), new Color(.025f, .035f, .065f, .98f));
            Label("LEVEL CLEAR", 80, cyan, new Vector2(800, 130), new Vector2(0, 190), modal).fontStyle = FontStyle.Bold;
            Label(elapsed.ToString("00.00") + " SEC  ·  " + deaths + " FALLS", 28, Color.white, new Vector2(760, 70), new Vector2(0, 60), modal);
            Button("PLAY AGAIN", new Vector2(650, 100), new Vector2(0, -80), yellow, modal, () => StartPlay(currentMap, returnToEditor));
            Button(returnToEditor ? "BACK TO MAKER" : "BACK TO MENU", new Vector2(650, 100), new Vector2(0, -205), panelColor, modal,
                () => { if (returnToEditor) ShowEditor(); else ShowHome(); });
            if (!string.IsNullOrEmpty(currentMap.id) && currentMap.id != "training") StartCoroutine(RecordCompletion());
            if (returnToEditor) clearedRevision = draftRevision;
            Debug.Log("BOUNCELAB_LEVEL_CLEAR seconds=" + elapsed.ToString("0.00"));
        }

        private void ShowEditor()
        {
            NewPage(Mode.Editor);
            Label("MAP MAKER", 58, Color.white, new Vector2(650, 90), new Vector2(-120, 850), page).fontStyle = FontStyle.Bold;
            Button("×", new Vector2(90, 90), new Vector2(450, 850), quiet, page, ShowHome);
            nameInput = TextInput("MAP NAME", draft.name, new Vector2(440, 80), new Vector2(-230, 755), page);
            authorInput = TextInput("MAKER NAME", draft.author, new Vector2(440, 80), new Vector2(230, 755), page);
            board = Panel("Editor Grid", page, new Vector2(640, 1024), new Vector2(0, 120), new Color(.035f, .05f, .08f));
            editorCells = new Image[MapRules.Width * MapRules.Height];
            const float editorCell = 64;
            for (int y = 0; y < MapRules.Height; y++)
            for (int x = 0; x < MapRules.Width; x++)
            {
                int captured = MapRules.Index(x, y);
                var cellImage = Box("Cell", board, new Vector2(editorCell - 2, editorCell - 2),
                    new Vector2((x - 4.5f) * editorCell, (y - 7.5f) * editorCell), gridColor);
                cellImage.raycastTarget = true;
                var button = cellImage.gameObject.AddComponent<UnityEngine.UI.Button>();
                button.targetGraphic = cellImage;
                button.onClick.AddListener(() => Paint(captured));
                editorCells[captured] = cellImage;
                PaintVisual(captured);
            }
            string[] names = { "ERASE", "BLOCK", "SPIKE", "GOAL", "SPRING", "START" };
            for (int i = 0; i < names.Length; i++)
            {
                int captured = i;
                Button(names[i], new Vector2(150, 80), new Vector2((i - 2.5f) * 165, -465),
                    TileColor(i), page, () => { brush = captured; UpdateBrushLabel(); });
            }
            brushText = Label("", 21, cyan, new Vector2(850, 45), new Vector2(0, -535), page);
            UpdateBrushLabel();
            statusText = Label("TAP A TILE TO PAINT", 21, quiet, new Vector2(900, 55), new Vector2(0, -595), page);
            Button("TEST", new Vector2(270, 105), new Vector2(-300, -720), yellow, page, TestDraft);
            Button("SAVE", new Vector2(270, 105), new Vector2(0, -720), panelColor, page, SaveDraftFromFields);
            Button("UPLOAD", new Vector2(270, 105), new Vector2(300, -720), cyan, page, UploadDraft);
            Button("RESET MAP", new Vector2(350, 70), new Vector2(0, -835), quiet, page, ResetDraft);
        }

        private void Paint(int index)
        {
            if (brush == MapRules.Spawn || brush == MapRules.Goal)
                for (int i = 0; i < draft.tiles.Length; i++)
                    if (draft.tiles[i] == brush) { draft.tiles[i] = MapRules.Empty; PaintVisual(i); }
            draft.tiles[index] = brush;
            draftRevision++;
            sound.PlayPaint();
            PaintVisual(index);
            SaveDraftFromFields();
        }

        private void PaintVisual(int index)
        {
            if (editorCells == null || editorCells[index] == null) return;
            int tile = draft.tiles[index];
            editorCells[index].color = TileColor(tile);
            var marker = editorCells[index].transform.childCount > 0 ? editorCells[index].transform.GetChild(0).GetComponent<Text>() :
                Label("", 25, Color.white, editorCells[index].rectTransform.sizeDelta, Vector2.zero, editorCells[index].rectTransform);
            marker.text = TileMark(tile);
        }

        private void UpdateBrushLabel()
        {
            if (brushText != null) brushText.text = "SELECTED: " + TileMark(brush) + " " + TileName(brush);
        }

        private void SaveDraftFromFields()
        {
            if (nameInput != null) draft.name = CleanName(nameInput.text, "UNTITLED");
            if (authorInput != null) draft.author = CleanName(authorInput.text, "MAKER");
            PlayerPrefs.SetString(DraftKey, JsonUtility.ToJson(draft));
            PlayerPrefs.Save();
            if (statusText != null) statusText.text = "SAVED ON THIS DEVICE";
        }

        private void TestDraft()
        {
            SaveDraftFromFields();
            string error = MapRules.Validate(draft);
            if (error.Length > 0) { statusText.text = error; statusText.color = coral; return; }
            StartPlay(draft, true);
        }

        private void ResetDraft()
        {
            draft = MapRules.Blank();
            draftRevision++;
            clearedRevision = -1;
            SaveDraftFromFields();
            ShowEditor();
        }

        private void UploadDraft()
        {
            SaveDraftFromFields();
            string error = MapRules.Validate(draft);
            if (error.Length > 0) { statusText.text = error; statusText.color = coral; return; }
            if (clearedRevision != draftRevision)
            { statusText.text = "CLEAR THIS VERSION IN TEST FIRST"; statusText.color = yellow; return; }
            statusText.text = "UPLOADING...";
            statusText.color = yellow;
            StartCoroutine(UploadMap(draft.Clone(), pageVersion));
        }

        private void ShowBrowse()
        {
            NewPage(Mode.Browse);
            Label("COMMUNITY MAPS", 54, Color.white, new Vector2(800, 90), new Vector2(-50, 830), page).fontStyle = FontStyle.Bold;
            Button("×", new Vector2(90, 90), new Vector2(450, 835), quiet, page, ShowHome);
            statusText = Label("LOADING MAPS...", 25, quiet, new Vector2(850, 70), new Vector2(0, 680), page);
            StartCoroutine(LoadMaps(pageVersion));
        }

        private void RenderMaps(MapData[] maps, int version)
        {
            if (mode != Mode.Browse || version != pageVersion) return;
            statusText.text = maps == null || maps.Length == 0 ? "NO MAPS YET — MAKE THE FIRST ONE" : "NEWEST MAPS";
            if (maps == null) return;
            int count = Mathf.Min(9, maps.Length);
            for (int i = 0; i < count; i++)
            {
                MapData selected = maps[i];
                float y = 565 - i * 145;
                var item = Button("", new Vector2(870, 120), new Vector2(0, y), panelColor, page, () => StartPlay(selected, false));
                Label(selected.name, 29, Color.white, new Vector2(540, 50), new Vector2(-120, 20), item).fontStyle = FontStyle.Bold;
                Label("BY " + selected.author + "   ·   " + selected.plays + " PLAYS   ·   " + selected.completions + " CLEARS",
                    18, quiet, new Vector2(740, 40), new Vector2(-20, -28), item);
                Label("▶", 36, cyan, new Vector2(70, 70), new Vector2(370, 0), item);
            }
            Button("REFRESH", new Vector2(340, 80), new Vector2(0, -800), quiet, page, ShowBrowse);
        }

        private IEnumerator ResolveApi(Action<bool> done)
        {
            using (var request = UnityWebRequest.Get(ApiConfigUrl + "?t=" + DateTimeOffset.UtcNow.ToUnixTimeSeconds()))
            {
                request.timeout = 8;
                yield return request.SendWebRequest();
                bool ok = request.result == UnityWebRequest.Result.Success;
                if (ok)
                {
                    var config = JsonUtility.FromJson<ApiConfig>(request.downloadHandler.text);
                    apiBase = config == null || config.baseUrl == null ? "" : config.baseUrl.TrimEnd('/');
                    ok = apiBase.StartsWith("https://");
                }
                if (mode == Mode.Home && statusText != null)
                {
                    statusText.text = ok ? "SERVER ONLINE" : "SERVER OFFLINE · TRAINING STILL WORKS";
                    statusText.color = ok ? cyan : coral;
                }
                if (done != null) done(ok);
            }
        }

        private IEnumerator EnsureApi(Action<bool> continuation)
        {
            if (apiBase.Length > 0) { continuation(true); yield break; }
            yield return ResolveApi(continuation);
        }

        private IEnumerator LoadMaps(int version)
        {
            bool ready = false;
            yield return EnsureApi(value => ready = value);
            if (!ready)
            {
                if (mode == Mode.Browse && version == pageVersion) { statusText.text = "MAP SERVER IS OFFLINE"; statusText.color = coral; }
                yield break;
            }
            using (var request = UnityWebRequest.Get(apiBase + "/api/maps?limit=9"))
            {
                request.timeout = 10;
                yield return request.SendWebRequest();
                if (mode != Mode.Browse || version != pageVersion) yield break;
                if (request.result != UnityWebRequest.Result.Success)
                { statusText.text = "COULD NOT LOAD MAPS"; statusText.color = coral; yield break; }
                var response = JsonUtility.FromJson<MapListResponse>(request.downloadHandler.text);
                RenderMaps(response == null ? null : response.maps, version);
            }
        }

        private IEnumerator UploadMap(MapData map, int version)
        {
            bool ready = false;
            yield return EnsureApi(value => ready = value);
            if (!ready) { SetEditorStatus(version, "MAP SERVER IS OFFLINE", coral); yield break; }
            byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(map));
            using (var request = new UnityWebRequest(apiBase + "/api/maps", "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 12;
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    var response = JsonUtility.FromJson<UploadResponse>(request.downloadHandler.text);
                    SetEditorStatus(version, "PUBLISHED · ID " + (response == null ? "OK" : response.id), cyan);
                }
                else SetEditorStatus(version, request.responseCode == 429 ? "UPLOAD LIMIT REACHED · TRY LATER" : "UPLOAD FAILED", coral);
            }
        }

        private IEnumerator RecordCompletion()
        {
            if (apiBase.Length == 0) yield break;
            byte[] body = Encoding.UTF8.GetBytes("{\"seconds\":" + elapsed.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + "}");
            using (var request = new UnityWebRequest(apiBase + "/api/maps/" + UnityWebRequest.EscapeURL(currentMap.id) + "/complete", "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(body);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 6;
                yield return request.SendWebRequest();
            }
        }

        private void SetEditorStatus(int version, string text, Color color)
        {
            if (mode != Mode.Editor || version != pageVersion || statusText == null) return;
            statusText.text = text; statusText.color = color;
        }

        private void LoadDraft()
        {
            string json = PlayerPrefs.GetString(DraftKey, "");
            try { draft = string.IsNullOrEmpty(json) ? MapRules.Blank() : JsonUtility.FromJson<MapData>(json); }
            catch { draft = MapRules.Blank(); }
            if (draft == null || draft.tiles == null || draft.tiles.Length != MapRules.Width * MapRules.Height) draft = MapRules.Blank();
        }

        private void DrawTile(int tile, int x, int y, RectTransform parent, float size, bool editor)
        {
            Vector2 position = new Vector2((x - 4.5f) * Cell, (y - 7.5f) * Cell);
            var image = Box(TileName(tile), parent, new Vector2(size, size), position, TileColor(tile));
            if (tile == MapRules.Spike) image.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            if (tile == MapRules.Spring) Box("Spring Core", image.rectTransform, new Vector2(size * .7f, 8), Vector2.zero, background);
            if (tile == MapRules.Goal) Label("GO", 23, background, image.rectTransform.sizeDelta, Vector2.zero, image.rectTransform).fontStyle = FontStyle.Bold;
        }

        private Color TileColor(int tile)
        {
            switch (tile)
            {
                case MapRules.Block: return new Color(.16f, .22f, .31f);
                case MapRules.Spike: return coral;
                case MapRules.Goal: return cyan;
                case MapRules.Spring: return yellow;
                case MapRules.Spawn: return new Color(.42f, .35f, 1f);
                default: return new Color(.045f, .06f, .095f);
            }
        }

        private static string TileMark(int tile)
        {
            switch (tile) { case 1: return "■"; case 2: return "◆"; case 3: return "G"; case 4: return "═"; case 5: return "S"; default: return "·"; }
        }
        private static string TileName(int tile)
        {
            switch (tile) { case 1: return "BLOCK"; case 2: return "SPIKE"; case 3: return "GOAL"; case 4: return "SPRING"; case 5: return "START"; default: return "ERASE"; }
        }
        private static string CleanName(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            value = value.Trim().Replace("<", "").Replace(">", "");
            return value.Length > 28 ? value.Substring(0, 28) : value;
        }

        private void Flash(Vector2 position, Color color)
        {
            var pulse = Box("Pulse", board, new Vector2(Cell, 8), position, color);
            Destroy(pulse.gameObject, .12f);
        }

        private RectTransform Panel(string name, RectTransform parent, Vector2 size, Vector2 position, Color color)
        { return Box(name, parent, size, position, color).rectTransform; }

        private Image Box(string name, RectTransform parent, Vector2 size, Vector2 position, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var image = obj.GetComponent<Image>(); image.color = color; image.raycastTarget = false;
            var rect = image.rectTransform; rect.SetParent(parent, false); rect.sizeDelta = size; rect.anchoredPosition = position;
            return image;
        }

        private Text Label(string value, int size, Color color, Vector2 dimensions, Vector2 position, RectTransform parent)
        {
            var obj = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var label = obj.GetComponent<Text>(); label.font = font; label.text = value; label.fontSize = size;
            label.alignment = TextAnchor.MiddleCenter; label.color = color; label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap; label.verticalOverflow = VerticalWrapMode.Truncate;
            var rect = label.rectTransform; rect.SetParent(parent, false); rect.sizeDelta = dimensions; rect.anchoredPosition = position;
            return label;
        }

        private RectTransform Button(string text, Vector2 size, Vector2 position, Color color, RectTransform parent, UnityEngine.Events.UnityAction action)
        {
            var image = Box("Button " + text, parent, size, position, color);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = Color.Lerp(color, Color.white, .18f); colors.pressedColor = Color.Lerp(color, background, .2f); button.colors = colors;
            if (action != null)
            {
                button.onClick.AddListener(() => { if (sound != null) sound.PlayUi(); });
                button.onClick.AddListener(action);
            }
            Label(text, Mathf.RoundToInt(Mathf.Clamp(size.y * .27f, 18, 34)), color == panelColor || color == quiet ? Color.white : background,
                size, Vector2.zero, image.rectTransform).fontStyle = FontStyle.Bold;
            return image.rectTransform;
        }

        private InputField TextInput(string placeholder, string value, Vector2 size, Vector2 position, RectTransform parent)
        {
            var image = Box("Input", parent, size, position, panelColor); image.raycastTarget = true;
            var input = image.gameObject.AddComponent<InputField>(); input.targetGraphic = image; input.characterLimit = 28;
            var text = Label(value, 25, Color.white, new Vector2(size.x - 35, size.y), Vector2.zero, image.rectTransform);
            text.alignment = TextAnchor.MiddleLeft; input.textComponent = text; input.text = value;
            var hint = Label(placeholder, 22, quiet, new Vector2(size.x - 35, size.y), Vector2.zero, image.rectTransform);
            hint.alignment = TextAnchor.MiddleLeft; input.placeholder = hint;
            return input;
        }

        private void Hold(RectTransform target, int value)
        {
            var trigger = target.gameObject.AddComponent<EventTrigger>();
            AddTrigger(trigger, EventTriggerType.PointerDown, _ => control = value);
            AddTrigger(trigger, EventTriggerType.PointerUp, _ => { if (control == value) control = 0; });
            AddTrigger(trigger, EventTriggerType.PointerExit, _ => { if (control == value) control = 0; });
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> action)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(action); trigger.triggers.Add(entry);
        }
        private static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    }
}
