// GEAR RUNNER - single-file Unity port (2D). No assets, prefabs or physics needed.
// SETUP: New 2D project -> open a scene -> create an Empty GameObject -> add this script -> press Play.
// Works with both the old Input Manager and the new Input System package.
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class GearRunner : MonoBehaviour
{
    const float W = 800f, H = 400f, GY = 320f, U = 40f; // game px, ground line, px per Unity unit
    enum S { Start, Play, Dead }

    class Ob  { public string k; public float x, y, w, h, r, b; public int ph; public Transform tr, spin, steam; public SpriteRenderer steamSr; }
    class Gr  { public float x, y; public Transform tr, spin; }
    class Pit { public float x, w; public Transform tr; }

    Sprite sqS, circS, gearS, triS;
    Transform root, player, wheel;
    Camera cam;
    Rect view;
    S state = S.Start;
    float px, py, vy, ph = 38f, speed, dist, shake;
    int jumps, spawn, t, score, best;
    bool duck, jumpQ, kl, kr, kd;
    readonly List<Ob> obs = new List<Ob>();
    readonly List<Pit> pits = new List<Pit>();
    readonly List<Gr> gears = new List<Gr>();
    readonly List<Transform[]> bgGears = new List<Transform[]>();
    readonly float[][] bgData = {
        new[]{120f,90f,70f,.4f,.15f}, new[]{420f,60f,110f,-.3f,.22f},
        new[]{700f,140f,60f,.5f,.15f}, new[]{950f,80f,90f,-.4f,.2f} };
    Transform[][] sky = new Transform[2][];   // [layer][building*2 + chimney]
    Transform[] lamps = new Transform[4];
    Transform[] rivets = new Transform[44];

    // ---------- helpers ----------
    static Color C(string hex) { Color c; ColorUtility.TryParseHtmlString(hex, out c); return c; }
    static Vector3 L(float x, float y) { return new Vector3(x / U, -y / U, 0f); }

    Sprite Make(System.Func<float, float, bool> f)
    {
        const int N = 64;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var px = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
                px[y * N + x] = f((x + .5f) / N * 2 - 1, (y + .5f) / N * 2 - 1) ? Color.white : new Color(1, 1, 1, 0);
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(.5f, .5f), N);
    }

    // sprite child at local px offset (cx, cy-down) with px size (w, h)
    SpriteRenderer R(Transform par, Sprite s, string col, float cx, float cy, float w, float h, int order)
    {
        var g = new GameObject("s");
        g.transform.SetParent(par, false);
        g.transform.localPosition = L(cx, cy);
        g.transform.localScale = new Vector3(w / U, h / U, 1f);
        var r = g.AddComponent<SpriteRenderer>();
        r.sprite = s; r.color = C(col); r.sortingOrder = order;
        return r;
    }

    Transform Node(float x, float y)
    {
        var g = new GameObject("n");
        g.transform.SetParent(root, false);
        g.transform.localPosition = L(x, y);
        return g.transform;
    }

    static void SetRect(Transform tr, float cx, float cy, float w, float h)
    {
        tr.localPosition = L(cx, cy);
        tr.localScale = new Vector3(w / U, h / U, 1f);
    }

    // ---------- setup ----------
    void Awake()
    {
        Time.fixedDeltaTime = 1f / 60f;
        best = PlayerPrefs.GetInt("gr_best", 0);

        sqS = Make((u, v) => true);
        circS = Make((u, v) => u * u + v * v <= 1f);
        triS = Make((u, v) => Mathf.Abs(u) <= (1f - v) / 2f);
        gearS = Make((u, v) =>
        {
            float r = Mathf.Sqrt(u * u + v * v), a = Mathf.Atan2(v, u);
            return r <= (Mathf.Repeat(a / (2f * Mathf.PI) * 8f, 1f) < .5f ? 1f : .78f);
        });

        cam = Camera.main;
        if (cam == null) { var cg = new GameObject("Main Camera"); cam = cg.AddComponent<Camera>(); cg.tag = "MainCamera"; }
        cam.orthographic = true; cam.orthographicSize = 5f;
        cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = C("#23272c");
        cam.transform.position = new Vector3(10f, 5f, -10f); cam.depth = 0;
        var bc = new GameObject("LetterboxCam").AddComponent<Camera>();
        bc.clearFlags = CameraClearFlags.SolidColor; bc.backgroundColor = Color.black; bc.cullingMask = 0; bc.depth = -10;

        root = new GameObject("World").transform;
        root.position = new Vector3(0f, H / U, 0f);

        BuildBackground();
        BuildPlayer();
        ResetGame();
    }

    void BuildBackground()
    {
        // sky gradient
        var gt = new Texture2D(1, 64, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < 64; y++)
        {
            float v = 1f - y / 63f;
            Color c = v < .52f ? Color.Lerp(C("#14171b"), C("#2e2b2a"), v / .52f)
                               : Color.Lerp(C("#2e2b2a"), C("#5a3a22"), Mathf.Clamp01((v - .52f) / .28f));
            gt.SetPixel(0, y, c);
        }
        gt.Apply();
        var gs = Sprite.Create(gt, new Rect(0, 0, 1, 64), new Vector2(.5f, .5f), 64);
        var sr = R(root, gs, "#ffffff", W / 2, H / 2, W * 64f, H, -100);
        sr.color = Color.white;

        // big rotating gears
        foreach (var d in bgData)
        {
            var g = R(root, gearS, "#2d3238", 0, 0, d[2] * 2, d[2] * 2, -90);
            var hub = R(g.transform, circS, "#23272c", 0, 0, .6f * 1f, .6f * 1f, -89);
            bgGears.Add(new[] { g.transform });
            hub.transform.localScale = new Vector3(.3f, .3f, 1f) * 1f;
        }

        // skyline (2 parallax layers)
        for (int l = 0; l < 2; l++)
        {
            sky[l] = new Transform[24];
            for (int i = 0; i < 12; i++)
            {
                sky[l][i * 2] = R(root, sqS, l == 0 ? "#1d2024" : "#25282d", 0, 0, 1, 1, -80 + l * 5).transform;
                sky[l][i * 2 + 1] = R(root, sqS, l == 0 ? "#1d2024" : "#25282d", 0, 0, 1, 1, -80 + l * 5).transform;
            }
        }

        // ceiling rail + lamps
        R(root, sqS, "#2b3036", W / 2, 19, W, 38, -60);
        R(root, sqS, "#3a4047", W / 2, 33, W, 10, -59);
        for (int k = 0; k < 4; k++)
        {
            var n = Node(0, 0); lamps[k] = n;
            R(n, sqS, "#4a525b", 0, 51, 2, 26, -58);
            R(n, circS, "#f4b942", 0, 68, 12, 12, -57);
            var glow = R(n, circS, "#f4b942", 0, 68, 140, 140, -61); glow.color = new Color(.96f, .72f, .26f, .14f);
        }

        // ground
        R(root, sqS, "#4a525b", W / 2, GY + (H - GY) / 2, W, H - GY, 0);
        R(root, sqS, "#e8892b", W / 2, GY + 2, W, 4, 1);
        for (int i = 0; i < 44; i++)
            rivets[i] = R(root, circS, "#6a737d", 0, i < 22 ? GY + 22 : GY + 58, 6, 6, 1).transform;
    }

    void BuildPlayer()
    {
        player = Node(0, 0);
        R(player, sqS, "#e8892b", 13, -21, 26, 18, 10);          // body
        R(player, sqS, "#b8b2a8", 13, -31, 22, 14, 11);          // head
        R(player, sqS, "#4af0ff", 18, -31.5f, 8, 5, 12);         // eye
        R(player, sqS, "#4a525b", 13, -41.5f, 2, 7, 10);         // antenna
        R(player, circS, "#c0392b", 13, -46, 6, 6, 11);
        var w = R(player, gearS, "#d9dde2", 13, -9, 20, 20, 12); wheel = w.transform;
        R(wheel, circS, "#23272c", 0, 0, 1, 1, 13).transform.localScale = new Vector3(.3f, .3f, 1f);
    }

    // ---------- game state ----------
    void ResetGame()
    {
        foreach (var o in obs) Destroy(o.tr.gameObject); obs.Clear();
        foreach (var p in pits) Destroy(p.tr.gameObject); pits.Clear();
        foreach (var g in gears) Destroy(g.tr.gameObject); gears.Clear();
        px = 110f; py = GY; vy = 0f; jumps = 0; duck = false;
        speed = 3.4f; dist = 0f; score = 0; spawn = 70;
    }

    void Jump()
    {
        if (state != S.Play)
        {
            if (state == S.Dead && t < 20) return;
            ResetGame(); state = S.Play; t = 0; return;
        }
        if (jumps < 2) { vy = jumps > 0 ? -10.5f : -13f; jumps++; }
    }

    void Die()
    {
        state = S.Dead; t = 0; shake = 12f;
        if (score > best) { best = score; PlayerPrefs.SetInt("gr_best", best); PlayerPrefs.Save(); }
    }

    // ---------- spawning ----------
    void AddPit(float x, float w)
    {
        var tr = Node(x, GY);
        R(tr, sqS, "#14171b", w / 2, (H - GY) / 2, w, H - GY, 2);
        R(tr, sqS, "#e8892b", -2, 5, 4, 10, 3);
        R(tr, sqS, "#e8892b", w + 2, 5, 4, 10, 3);
        pits.Add(new Pit { x = x, w = w, tr = tr });
    }

    void AddGear(float x, float y)
    {
        var tr = Node(x, y);
        var s = R(tr, gearS, "#f4b942", 0, 0, 22, 22, 6);
        R(s.transform, circS, "#23272c", 0, 0, 1, 1, 7).transform.localScale = new Vector3(.32f, .32f, 1f);
        gears.Add(new Gr { x = x, y = y, tr = tr, spin = s.transform });
    }

    void Spike(float x, float w)
    {
        var o = new Ob { k = "spike", x = x, w = w, h = 22f, tr = Node(x, GY) };
        int n = Mathf.Max(1, Mathf.RoundToInt(w / 22f));
        for (int i = 0; i < n; i++) R(o.tr, triS, "#c9ced4", (i + .5f) * w / n, -11, w / n, 22, 5);
        obs.Add(o);
    }

    void Saw(float x, float y)
    {
        var o = new Ob { k = "saw", x = x, y = y, r = 18f, tr = Node(x, y) };
        R(o.tr, sqS, "#4a525b", 0, -(y - 28f) / 2f, 4, y - 28f, 4);
        var g = R(o.tr, gearS, "#d9dde2", 0, 0, 36, 36, 5); o.spin = g.transform;
        R(g.transform, circS, "#c0392b", 0, 0, 1, 1, 6).transform.localScale = new Vector3(.28f, .28f, 1f);
        obs.Add(o);
    }

    void Crate(float x)
    {
        var o = new Ob { k = "crate", x = x, w = 38f, h = 38f, tr = Node(x, GY) };
        R(o.tr, sqS, "#e8892b", 19, -19, 38, 38, 5);
        R(o.tr, sqS, "#8a5a2b", 19, -19, 32, 32, 6);
        obs.Add(o);
    }

    void Crusher(float x, float b)
    {
        var o = new Ob { k = "crusher", x = x, w = 44f, b = b, tr = Node(x, 0) };
        R(o.tr, sqS, "#4a525b", 22, (28f + b) / 2f, 10, b - 28f, 4);
        R(o.tr, sqS, "#c0392b", 22, b - 7, 44, 14, 5);
        for (int i = 0; i < 4; i++) R(o.tr, triS, "#d9dde2", (i + .5f) * 11, b + 4, 11, 8, 5).transform.localRotation = Quaternion.Euler(0, 0, 180);
        obs.Add(o);
    }

    void Vent(float x)
    {
        var o = new Ob { k = "vent", x = x, w = 36f, ph = Random.Range(0, 120), tr = Node(x, GY) };
        R(o.tr, sqS, "#6a737d", 18, -4, 36, 8, 5);
        o.steamSr = R(o.tr, sqS, "#ebf0f5", 18, -49, 28, 82, 6);
        o.steam = o.steamSr.transform;
        obs.Add(o);
    }

    void AddSpawn()
    {
        float x = W + 60f, r = Random.value;
        if (r < .14f) { float w = 70f + Random.value * 40f; AddPit(x, w); if (Random.value < .7f) AddGear(x + w / 2, GY - 110); }
        else if (r < .27f) { Spike(x, 34); AddGear(x + 17, GY - 80); }
        else if (r < .38f) { Spike(x, 68); AddGear(x + 34, GY - 115); }
        else if (r < .5f) Saw(x, GY - (18f + Random.value * 50f));
        else if (r < .58f) { Saw(x, GY - 18f); Saw(x + 55, GY - 80f); }
        else if (r < .69f) { Crate(x); AddGear(x + 19, GY - 95); }
        else if (r < .79f) Crusher(x, Random.value < .5f ? GY - 52f : GY - 28f);
        else if (r < .9f) Vent(x);
        else for (int i = 0; i < 4; i++) AddGear(x + i * 36, GY - 40 - Mathf.Sin(i / 3f * Mathf.PI) * 50);
        spawn = (int)(Mathf.Max(60f, 125f - speed * 5f) + Random.value * 50f);
    }

    bool OverPit()
    {
        foreach (var q in pits) if (px + 4 > q.x && px + 22 < q.x + q.w) return true;
        return false;
    }

    // ---------- input (read every frame) ----------
    void Update()
    {
        UpdateView();
        kl = kr = kd = false;
#if ENABLE_INPUT_SYSTEM
        var k = Keyboard.current;
        if (k != null)
        {
            if (k.spaceKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame) jumpQ = true;
            kl |= k.leftArrowKey.isPressed || k.aKey.isPressed;
            kr |= k.rightArrowKey.isPressed || k.dKey.isPressed;
            kd |= k.downArrowKey.isPressed || k.sKey.isPressed;
        }
        if (Mouse.current != null) Pointer(Mouse.current.position.ReadValue(), Mouse.current.leftButton.isPressed, Mouse.current.leftButton.wasPressedThisFrame);
        if (Touchscreen.current != null)
            foreach (var tc in Touchscreen.current.touches) Pointer(tc.position.ReadValue(), tc.press.isPressed, tc.press.wasPressedThisFrame);
#else
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) jumpQ = true;
        kl |= Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A);
        kr |= Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D);
        kd |= Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S);
        if (Input.GetMouseButton(0) || Input.GetMouseButtonDown(0)) Pointer(Input.mousePosition, Input.GetMouseButton(0), Input.GetMouseButtonDown(0));
        for (int i = 0; i < Input.touchCount; i++)
        {
            var tc = Input.GetTouch(i);
            Pointer(tc.position, tc.phase != TouchPhase.Ended && tc.phase != TouchPhase.Canceled, tc.phase == TouchPhase.Began);
        }
#endif
    }

    // touch / mouse zones: left edge = move left, right edge = move right, centre = jump, bottom centre = duck
    void Pointer(Vector2 pos, bool held, bool down)
    {
        if (!held && !down) return;
        float fx = (pos.x - view.x) / view.width, fy = 1f - (pos.y - view.y) / view.height;
        if (fx < 0 || fx > 1 || fy < 0 || fy > 1) return;
        char z = fx < .25f ? 'l' : fx > .75f ? 'r' : fy > .6f ? 'd' : 'j';
        if (down && (state != S.Play || z == 'j')) jumpQ = true;
        if (state == S.Play && held) { if (z == 'l') kl = true; else if (z == 'r') kr = true; else if (z == 'd') kd = true; }
    }

    void UpdateView()
    {
        float sa = (float)Screen.width / Screen.height, ta = 2f;
        Rect r = sa > ta ? new Rect((1f - ta / sa) / 2f, 0f, ta / sa, 1f) : new Rect(0f, (1f - sa / ta) / 2f, 1f, sa / ta);
        cam.rect = r;
        view = new Rect(r.x * Screen.width, r.y * Screen.height, r.width * Screen.width, r.height * Screen.height);
    }

    // ---------- fixed 60 Hz simulation ----------
    void FixedUpdate()
    {
        t++;
        if (jumpQ) { jumpQ = false; Jump(); }
        if (state == S.Play) Step();
        Render();
    }

    void Step()
    {
        dist += speed;
        speed = Mathf.Min(7f, 3.4f + dist / 4000f);
        score = (int)(dist / 40f);
        if (--spawn <= 0) AddSpawn();

        bool gnd = py >= GY - 1f && vy == 0f;
        duck = kd && gnd; ph = duck ? 23f : 38f;
        if (kd && !gnd) vy += 1.2f;
        float mv = (kl ? -1f : 0f) + (kr ? 1f : 0f);
        px = Mathf.Clamp(px + mv * (duck ? 2f : 3.6f), 30f, 420f);
        vy += .62f; py += vy;
        if (py >= GY && !OverPit() && py - vy <= GY + 2f) { py = GY; vy = 0f; jumps = 0; }
        if (py > H + 60f) { Die(); return; }

        foreach (var o in obs) o.x -= speed;
        foreach (var p in pits) p.x -= speed;
        foreach (var g in gears) g.x -= speed;
        for (int i = obs.Count - 1; i >= 0; i--) if (obs[i].x < -100f) { Destroy(obs[i].tr.gameObject); obs.RemoveAt(i); }
        for (int i = pits.Count - 1; i >= 0; i--) if (pits[i].x + pits[i].w < -50f) { Destroy(pits[i].tr.gameObject); pits.RemoveAt(i); }
        for (int i = gears.Count - 1; i >= 0; i--) if (gears[i].x < -50f) { Destroy(gears[i].tr.gameObject); gears.RemoveAt(i); }

        float Lf = px + 3f, Rt = px + 23f, Tp = py - ph + 3f, Bt = py;
        foreach (var o in obs)
        {
            bool hit = false;
            switch (o.k)
            {
                case "spike":   hit = Rt > o.x + 4 && Lf < o.x + o.w - 4 && Bt > GY - o.h + 4; break;
                case "crate":   hit = Rt > o.x + 3 && Lf < o.x + o.w - 3 && Bt > GY - o.h + 3; break;
                case "crusher": hit = Rt > o.x + 3 && Lf < o.x + o.w - 3 && Tp < o.b; break;
                case "vent":    hit = (t + o.ph) % 120 < 50 && Rt > o.x + 4 && Lf < o.x + o.w - 4; break;
                case "saw":
                    float cx = Mathf.Clamp(o.x, Lf, Rt), cy = Mathf.Clamp(o.y, Tp, Bt);
                    hit = (cx - o.x) * (cx - o.x) + (cy - o.y) * (cy - o.y) < (o.r - 3) * (o.r - 3); break;
            }
            if (hit) { Die(); return; }
        }
        for (int i = gears.Count - 1; i >= 0; i--)
        {
            var g = gears[i];
            if (Rt > g.x - 10 && Lf < g.x + 10 && Bt > g.y - 10 && Tp < g.y + 10)
            { dist += 200f; Destroy(g.tr.gameObject); gears.RemoveAt(i); }
        }
    }

    // ---------- rendering ----------
    void Render()
    {
        float off = state == S.Start ? t * 3f : dist;

        // background gears
        for (int i = 0; i < bgData.Length; i++)
        {
            var d = bgData[i];
            float bx = ((d[0] - off * d[4]) % 1100f + 1100f) % 1100f - 150f;
            bgGears[i][0].localPosition = L(bx, d[1]);
            bgGears[i][0].localRotation = Quaternion.Euler(0, 0, -off * .01f * d[3] * (i % 2 == 1 ? 1 : -1) * 3f * Mathf.Rad2Deg);
        }

        // skyline
        for (int l = 0; l < 2; l++)
        {
            float par = l == 0 ? .06f : .16f; int seed = l == 0 ? 1 : 4; const float cw = 90f;
            float o = ((off * par) % cw + cw) % cw; int i0 = Mathf.FloorToInt(off * par / cw);
            for (int k = -1; k < 11; k++)
            {
                int id = i0 + k; float h = 50f + (((id * 37 + seed) % 5) + 5) % 5 * 24f, x = k * cw - o;
                SetRect(sky[l][(k + 1) * 2], x + (cw - 8) / 2, GY - h / 2, cw - 8, h);
                bool chim = ((((id + seed) % 3) + 3) % 3) == 0;
                SetRect(sky[l][(k + 1) * 2 + 1], x + 29, GY - h - 20, chim ? 14 : 0, chim ? 40 : 0);
            }
        }

        // lamps
        for (int k = 0; k < 4; k++) lamps[k].localPosition = L(((k * 260f - off * .35f) % 1040f + 1040f) % 1040f - 120f, 0);

        // ground rivets
        float o2 = Mathf.Floor(off) % 40f;
        for (int i = 0; i < 44; i++) { var p = rivets[i].localPosition; rivets[i].localPosition = new Vector3((-o2 + (i % 22) * 40f) / U, p.y, 0); }

        // entities
        foreach (var q in pits) q.tr.localPosition = L(q.x, GY);
        foreach (var g in gears) { g.tr.localPosition = L(g.x, g.y); g.spin.localRotation = Quaternion.Euler(0, 0, -t * .08f * Mathf.Rad2Deg); }
        foreach (var o in obs)
        {
            float oy = o.k == "saw" ? o.y : o.k == "crusher" ? 0f : GY;
            o.tr.localPosition = L(o.x, oy);
            if (o.k == "saw") o.spin.localRotation = Quaternion.Euler(0, 0, -t * .25f * Mathf.Rad2Deg);
            if (o.k == "vent")
            {
                int p = (t + o.ph) % 120;
                if (p < 50) { SetRect(o.steam, 18, -49, 28, 82); o.steamSr.color = new Color(.92f, .94f, .96f, .75f); }
                else if (p > 90) { SetRect(o.steam, 18, -16, 16, 16); o.steamSr.color = new Color(.92f, .94f, .96f, .35f); }
                else o.steamSr.color = new Color(1, 1, 1, 0);
                o.steam.localPosition = new Vector3(o.steam.localPosition.x, o.steam.localPosition.y, 0);
            }
        }

        // player
        player.gameObject.SetActive(state != S.Dead || t < 30);
        player.localPosition = L(px, py);
        player.localScale = new Vector3(1f, duck ? .62f : 1f, 1f);
        bool air = py < GY - 1f;
        wheel.localRotation = Quaternion.Euler(0, 0, -(air ? t * .1f : t * .25f * speed / 5f) * Mathf.Rad2Deg);

        // camera shake
        if (shake > .05f) shake *= .85f; else shake = 0f;
        cam.transform.position = new Vector3(10f + (Random.value - .5f) * shake / U, 5f + (Random.value - .5f) * shake / U, -10f);
    }

    // ---------- HUD ----------
    void OnGUI()
    {
        float s = view.height / 400f, gx = view.x, gy = Screen.height - (view.y + view.height);
        var st = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = Mathf.Max(8, (int)(18 * s)) };
        st.normal.textColor = C("#ece7df");
        st.alignment = TextAnchor.UpperLeft;
        GUI.Label(new Rect(gx + 16 * s, gy + 12 * s, 300 * s, 30 * s), "DIST " + score + " m", st);
        st.alignment = TextAnchor.UpperRight;
        GUI.Label(new Rect(gx + view.width - 316 * s, gy + 12 * s, 300 * s, 30 * s), "BEST " + best + " m", st);
        if (state == S.Play) return;

        GUI.color = new Color(.08f, .09f, .1f, .72f);
        GUI.DrawTexture(new Rect(gx, gy + 110 * s, view.width, 130 * s), Texture2D.whiteTexture);
        GUI.color = Color.white;
        st.alignment = TextAnchor.UpperCenter;
        st.fontSize = Mathf.Max(8, (int)(34 * s)); st.normal.textColor = C("#e8892b");
        GUI.Label(new Rect(gx, gy + 122 * s, view.width, 50 * s), state == S.Start ? "GEAR RUNNER" : "SYSTEM FAILURE", st);
        st.fontStyle = FontStyle.Normal; st.fontSize = Mathf.Max(8, (int)(16 * s)); st.normal.textColor = C("#ece7df");
        GUI.Label(new Rect(gx, gy + 172 * s, view.width, 30 * s),
            state == S.Start ? "Press SPACE or click / tap to start" : "Distance " + score + " m - press SPACE or click / tap to retry", st);
        st.fontSize = Mathf.Max(8, (int)(13 * s)); st.normal.textColor = C("#9a9388");
        GUI.Label(new Rect(gx, gy + 200 * s, view.width, 30 * s),
            "Jump (twice = double jump)  |  Left/Right move  |  Down = duck under low crushers", st);
    }
}
