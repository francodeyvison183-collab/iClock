# Uploading iClock to GitHub

This directory is the complete local project root. It contains the source, documentation, GitHub templates, build script, release notes, and the prepared Windows download package.

## Create the public source repository

1. Create an empty public repository on GitHub. Do not initialize it with another README, license, or `.gitignore`.
2. In a terminal, change to this directory (`iClock-github`) and run:

   ```powershell
   git init
   git add .
   git commit -m "Prepare iClock 1.0.0"
   git branch -M main
   git remote add origin https://github.com/OWNER/REPOSITORY.git
   git push -u origin main
   ```

3. Replace `OWNER/REPOSITORY` with the actual GitHub account and repository name.

The `.gitignore` keeps compiled output and `release-assets` out of the source commit. The source repository still contains the build script and release instructions.

## Publish the user download

After pushing the source:

1. Create a tag named `v1.0.0` and push it, or create the release through GitHub's Releases page.
2. Use [release notes for v1.0.0](release-notes/v1.0.0.md).
3. Upload `release-assets/iClock-1.0.0-windows.zip` and `release-assets/iClock-1.0.0-SHA256SUMS.txt` as Release assets.
4. Publish the release. The README's Releases link will then take ordinary users to the download page.

The ZIP contains `iClock.exe`, a quick-start guide, and the MIT License. Users extract it and run the program; they do not need to build the source.
