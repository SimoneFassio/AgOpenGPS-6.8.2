# Fork Maintenance & Release Workflow

This repository is a customized fork of [AgOpenGPS-Official/AgOpenGPS](https://github.com/AgOpenGPS-Official/AgOpenGPS).

## Custom Modifications Maintained

1. **AgIO Secondary UDP Endpoint (`.137.255:8888`)**:
   - Files:
     - `SourceCode/AgIO/Source/Forms/UDP.designer.cs`: Defines `public IPEndPoint epModule2 = new IPEndPoint(IPAddress.Parse(... + "137.255"), 8888);` and forwards UDP data to `epModule2` inside `ReceiveFromLoopBack()`.
     - `SourceCode/AgIO/Source/Forms/FormUDP.cs`: Updates `mf.epModule2` IP address when the user changes subnet settings via `btnSendSubnet_Click()`.
2. **GitHub Actions Workflow Dispatch**:
   - Enables `workflow_dispatch` in `.github/workflows/build.yml` and `.github/workflows/release.yml` to trigger manual builds on any branch.

---

## Workflow: Updating to a Future Upstream Release

Follow these steps whenever official AgOpenGPS releases a new version (e.g. `6.8.7`, `6.9.0`, etc.).

### 1. Ensure Upstream Remote is Configured

Verify that the official upstream remote is set:
```bash
git remote -v
```
If `upstream` is not listed:
```bash
git remote add upstream https://github.com/AgOpenGPS-Official/AgOpenGPS.git
```

### 2. Fetch Latest Upstream Tags and Branches

```bash
git fetch upstream --tags
```

### 3. Create a New Branch for the Target Release

Official releases in AgOpenGPS are published as Git tags (e.g., `6.8.7`).
Create and check out a new branch based on that tag:
```bash
# Replace 6.8.7 with the new release tag
git checkout -b release/6.8.7 6.8.7
```

### 4. Cherry-Pick Custom Changes

Apply the custom commit from the previous release branch (e.g., `release/6.8.6`):
```bash
# Cherry-pick the custom commit
git cherry-pick <COMMIT_HASH_FROM_PREVIOUS_RELEASE>
```

> **Note**: Because the custom changes are isolated to AgIO UDP networking and workflow files, conflicts are extremely rare unless upstream completely rewrites `FormUDP.cs` or `UDP.designer.cs`. If a conflict occurs, resolve it and run `git cherry-pick --continue`.

### 5. Push the New Branch to GitHub

```bash
# Replace 6.8.7 with your new branch name
git push -u origin release/6.8.7
```

---

## How and Where to Run the Build Workflow

There are two GitHub Actions workflows available in this repository:

### Option A: Automatic Build & Release on Push (`release.yml`)
When you push any branch matching `release/*` (such as `release/6.8.6` or `release/6.8.7`):
- GitHub Actions automatically starts the **Build and release** workflow.
- When finished, it creates a new entry under your repo's **Releases** tab (`https://github.com/SimoneFassio/AgOpenGPS-6.8.2/releases`) containing `AgOpenGPS_<SemVer>.zip`.

### Option B: Manual Execution via GitHub UI (`workflow_dispatch`)
You can manually run a build at any time:
1. Go to your repository on GitHub: `https://github.com/SimoneFassio/AgOpenGPS-6.8.2`
2. Click on the **Actions** tab at the top.
3. In the left sidebar:
   - Click **Build** (from `build.yml`) for an artifact build, OR
   - Click **Build and release** (from `release.yml`) for a packaged release zip.
4. Click the **Run workflow** dropdown button on the right.
5. In **Use workflow from**, select your target branch (e.g., `release/6.8.6`).
6. Click the green **Run workflow** button.

### Downloading the Built Executable

- **From "Build" workflow**: Click on the completed workflow run. Scroll to the bottom to find the **Artifacts** section and download `AgOpenGPS`.
- **From "Build and release" workflow**: Go to the **Releases** page of your repository to download the generated `.zip` file.
