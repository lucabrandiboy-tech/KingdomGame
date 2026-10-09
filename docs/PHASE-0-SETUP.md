# Phase 0: Setup, step by step

> **Note (Phase 2):** this page is the record of the original setup. Since Phase 2 the game
> uses a smooth, realistic look: there is no Pixel Perfect Camera any more, tiles are 64 × 64
> pixels, and the Setup Check no longer checks the camera.

When you finish this page you will have:

- Unity 6.3 LTS installed
- The `KingdomGame` Unity project (2D, URP) living inside your private GitHub repo
- Git LFS storing art and sound
- Pixel-perfect camera and import settings for 32 × 32 pixel tiles
- A test brick wall on screen that proves the pixel settings work, saved on GitHub

It takes about an hour, mostly waiting for downloads. Do the steps in order. If
something doesn't look like this page says, stop and tell Claude what you see
(a screenshot is perfect).

**Words you'll see**

| Word | Meaning |
|---|---|
| **Unity Hub** | The launcher app. It installs Unity versions and opens projects. |
| **Unity Editor** | The actual program where you build the game. |
| **LTS** | "Long Term Support": a Unity version that gets bug fixes for 2 years. |
| **URP** | Universal Render Pipeline: the Unity graphics system that has 2D lights and shadows. |
| **Repo** (repository) | Your project's folder on GitHub, with its full history. |
| **Clone** | Download a repo to your computer so you can work on it. |
| **Commit** | Save a snapshot of your changes, with a short message. |
| **Push** | Upload your commits to GitHub. |
| **Git LFS** | Large File Storage. Pictures and sounds go there instead of in the normal repo history, which keeps the repo fast. |
| **.meta file** | Unity makes one next to every file in `Assets`. It holds that file's ID and settings. Always commit it together with the file. |

---

## Step 1. Install Unity 6.3 LTS

1. Open **Unity Hub** and sign in if it asks.
2. Click **Installs** on the left.
   - If you already see a version starting with **6000.3**, skip to step 1.5.
3. Click **Install Editor**. On the **Official releases** tab, find **Unity 6.3 (LTS)** and click **Install**.
4. On the **Add modules** screen tick:
   - **Microsoft Visual Studio Community** (the code editor; skip it if you already have Visual Studio)
   - **Windows Build Support (IL2CPP)** (for making the Steam .exe later)

   Then click **Install**/**Continue** and wait (it's a few GB).
5. Older Unity versions you already have can stay. They don't get in the way.

> Why 6.3 and not the newest number? 6.3 is the newest **LTS**: Unity fixes its
> bugs until December 2027 without changing how things work. Newer numbers
> (6.4, 6.5…) get new features but only short support. See `docs/DECISIONS.md`.

## Step 2. Clone the repo with GitHub Desktop

1. Make a folder **`C:\Dev`** (in File Explorer: This PC → Local Disk (C:) → right-click → New → Folder).
   Don't use Documents or Desktop if OneDrive syncs them: OneDrive fights with
   Unity's thousands of temporary files.
2. Open **GitHub Desktop** → **File → Clone repository…** → **GitHub.com** tab.
3. Pick **lucabrandiboy-tech/KingdomGame**.
4. Set **Local path** to `C:\Dev\KingdomGame` and click **Clone**.
5. If GitHub Desktop says the repo uses Git LFS and offers **Initialize Git LFS**, click it.

You now have `C:\Dev\KingdomGame` with folders like `Assets`, `docs` and `tools`.
It isn't a Unity project yet; Unity's part comes next.

## Step 3. Create the Unity project (in a temporary folder)

Unity Hub can only create a project in a brand-new empty folder, so we create it
somewhere temporary and move it into the repo in step 4.

1. Unity Hub → **Projects** → **New project**.
2. At the top, set **Editor Version** to your **6000.3.x** version.
3. Pick the template **Universal 2D**. If it shows a download button, click it first.
4. On the right:
   - **Project name:** `KingdomGame`
   - **Location:** `C:\Dev\UnityTemp`
   - **Untick** "Connect to Unity Cloud" and "Use Unity Version Control" if you see them. We use GitHub instead.
5. Click **Create project**. Unity opens (the first time takes several minutes).
6. When the Unity Editor has fully opened, **close it** (File → Exit).

## Step 4. Move the project into the repo

1. In File Explorer open `C:\Dev\UnityTemp\KingdomGame`.
2. Select **only these three folders**: `Assets`, `Packages`, `ProjectSettings`.
   Leave `Library`, `Logs`, `Temp`, `UserSettings` and any `.sln`/`.csproj` files behind.
   Unity rebuilds those by itself.
3. Copy them (**Ctrl+C**), open `C:\Dev\KingdomGame`, and paste (**Ctrl+V**).
   The `Assets` folders merge. If Windows asks about replacing files, choose **Skip these files**.
4. Unity Hub → **Projects** → the arrow next to **Add** → **Add project from disk** → choose `C:\Dev\KingdomGame` → **Open**.
5. In the Hub's project list, find the old `C:\Dev\UnityTemp\KingdomGame` entry → **⋯** → **Remove from list**.
   Then delete the `C:\Dev\UnityTemp` folder in File Explorer.

## Step 5. Open the project and run the setup check

1. In Unity Hub click the **KingdomGame** project (the `C:\Dev\KingdomGame` one).
   The first open takes a few minutes.
2. Unity may open an empty "Untitled" scene. In the **Project** window (bottom)
   go to **Assets → Scenes** and double-click **SampleScene**.
3. In the menu bar at the top, click **KingdomGame → Phase 0 Setup Check**.
   (No **KingdomGame** menu? Look at the **Console** window for red errors and
   send them to Claude.)
4. Click **Fix everything I can**. This:
   - adds a **Pixel Perfect Camera** to the Main Camera (32 pixels per unit, 640 × 360) and saves the scene
   - makes the test brick wall picture in `Assets/Art/Test` and puts it in the scene
   - turns off MSAA anti-aliasing (useless for pixel art) if it was on
   - switches Unity to text files and visible .meta files if they weren't already
5. Read the list. You want every line **PASS**. Things it can't fix by itself:

| Line | What to do |
|---|---|
| Unity 6.3 LTS: WARN/FAIL | You opened it with another version. Hub → Projects → click the version number next to KingdomGame → choose 6000.3.x. |
| Project is inside the KingdomGame Git repo: FAIL | You opened the temp copy. Redo step 4.4. |
| Git LFS is switched on: WARN | Fine for now. Step 7 double-checks it on github.com. |
| URP / 2D Renderer: FAIL | The project wasn't made from the **Universal 2D** template. Redo steps 3–4. |
| Input System package: FAIL | Follow the text in that line (Unity restarts), then run the check again. |

## Step 6. Look at the test wall

1. Click the **Game** tab (next to Scene). In its top bar, set the size dropdown to
   **Free Aspect**.
2. **Double-click the Game tab** so it fills the whole Unity window. (A bigger
   view makes the camera scale pixels up, which is what we want to test.
   Double-click the tab again later to shrink it back.)
3. Press **▶ Play** (top middle).
4. You should see a red-brown brick wall, 2 × 2 tiles, in the middle of the view.
   - **Good:** every brick pixel is a crisp square block (2 × 2, 3 × 3 or more screen
     pixels, depending on your monitor), all the same size, with hard edges.
     Zoom in with Windows Magnifier (**Windows key + plus**, **Windows key + Esc** to stop) to check.
   - **Bad:** blurry or smeared bricks, or pixels of different sizes. Tell Claude.
5. Press **▶** again to stop playing. Changes made while playing are thrown away.

## Step 7. Commit and push

1. Open **GitHub Desktop**. On the left you'll see the changed files: lots of
   `Assets/…`, `Packages/…` and `ProjectSettings/…` files, including `.meta` files.
   - You should **not** see `Library/`, `Temp/` or `Logs/`. If you do, stop and tell Claude:
     the `.gitignore` isn't working.
2. Bottom left, in **Summary**, type: `Phase 0: Unity 6.3 Universal 2D project, pixel camera, test wall`
3. Click **Commit to main**, then **Push origin** at the top.
4. Check it on GitHub:
   - Open `github.com/lucabrandiboy-tech/KingdomGame` → `Assets/Art/Test/TestBrickWall.png`.
     It should say **Stored with Git LFS**. That proves LFS works.
   - Click the **Actions** tab. The newest **Repo checks** run should get a green ✓ within a minute or two.
     A red ✗ means a file was committed wrong; click it and send Claude the red text.

## Step 8. Tell Claude

Send:

1. "Phase 0 works" (or what went wrong)
2. Your exact Unity version (Help → About Unity, or the number in Unity Hub)
3. A screenshot of the Setup Check window

Claude then reads your pushed project (the real Unity settings files), and Phase 1 (the
destruction prototype) starts once you confirm.

---

## Day-to-day from now on

- **Open the game:** Unity Hub → KingdomGame.
- **Save work to GitHub:** GitHub Desktop → write a summary → Commit to main → Push origin.
  Do it whenever something works.
- **Get Claude's changes:** GitHub Desktop → **Fetch origin** → **Pull origin**, *with
  Unity closed*. Then open Unity.
- **Never** delete or rename files in File Explorer while Unity is open. Do it
  inside Unity's Project window so the `.meta` files move along with them.
- **Always** commit a file together with its `.meta` file. The Repo checks action catches it if you forget.
