Steps to download and build V8:

1. Make sure you have depot_tools installed and environment variables setup correctly (including DEPOT_TOOLS_WIN_TOOLCHAIN):
   https://chromium.googlesource.com/chromium/src/+/master/docs/windows_build_instructions.md#install

2. Download the source into THIS folder (where this readme.txt file exists, so 'V8.NET-Proxy\V8\src\...').
   Follow instructions here: https://v8.dev/docs/source-code

3. Go here for steps on building the V8 source: https://v8.dev/docs/build
   Tip: In the 'tools' folder run 'gm.py' without parameters to see all architectures.

4. Compiled files will be in 'tools\dev\out' in a sub-folder for each architecture.

5. To get the static lib files for building V8.NET open the command prompt here: src\v8\tools\dev
   And execute this:
   * ninja -C out/x64.release
   * ninja -C out/ia32.release

You should now be ready to build V8.NET! :)
   
Staying up to date: https://v8.dev/docs/source-code#staying-up-to-date

If you continue to have issues, there's a good post on it here that may also help:
https://medium.com/dailyjs/how-to-build-v8-on-windows-and-not-go-mad-6347c69aacd4
