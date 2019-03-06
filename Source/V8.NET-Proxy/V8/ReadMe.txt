Steps to download and build V8:

1. Make sure you have depot_tools installed and environment variables setup correctly (including DEPOT_TOOLS_WIN_TOOLCHAIN):
   https://chromium.googlesource.com/chromium/src/+/master/docs/windows_build_instructions.md#install

   Make sure to set these environment variables for the tools:
   * DEPOT_TOOLS_WIN_TOOLCHAIN=0
   * GYP_MSVS_VERSION=2017

2. Download the source into 'C:\ProgramData\Google\V8\src\'.
   Follow instructions here: https://v8.dev/docs/source-code

   If you want to compile a specified version, execute:
   > git checkout #.#.### (where # is the version to checkout)
   > gclient sync
   

4. Go to "src\v8", open a command prompt, and execute this:
   > python tools\dev\v8gen.py x64.release
   > python tools\dev\v8gen.py ia32.release
   
   This will output files that can be compiled in the "src\v8\out.gn" folder.
   Tip: Run "python tools\dev\v8gen.py list" to see a list of possible build configurations.
		 
   More details on V8 source building is here: https://v8.dev/docs/build
   
5. Build files will be in 'v8\out.gn' in a sub-folder for each build configuration.
   For each configuration we need to update the args.gn file.  Run this:
   
   > gn args out.gn\x64.release
   
   Which will open notepad (or other text editor) to make changes. Use these settings:
    
     is_debug = false
     target_cpu = "x64"
     is_component_build = false
     v8_static_library = true
     use_custom_libcxx = false
     use_custom_libcxx_for_host = false
     v8_use_external_startup_data = false
     is_clang = false   
 
   After saving the changes and closing the editor the 'gn' script will re-generate some files.
   Do this again for the 32-bit version (don't forget to change "x64" to "x86" for "target_cpu":
	
   > gn args out.gn\ia32.release
 
   Replace "x64" with "x86" for "target_cpu" when updating "ia32.release\args.gn".
   If you do not set "is_clang" to false in the settings then VS will complain the .lib files are corrupt.
   
   If "v8_use_external_startup_data" is true, then V8 will start more quickly, but two .bin files in the
   out.gn\*.release folders will need to be included with the V8.Net output files.
   
   Follow these same steps for 'x64.debug' and 'ia32.debug' as well; however, make sure to set 'is_debug = true' instead.
   
6. To build, run these:
   * ninja -C out.gn/x64.debug v8
   * ninja -C out.gn/ia32.debug v8
   * ninja -C out.gn/x64.release v8
   * ninja -C out.gn/ia32.release v8

   Note: If you get an error that 'cctest' failed to compile, we don't need it, so ignore it.
         Adding 'v8' at the end of the command will skip 'cctest', so you may have missed that flag. ;)
 
7. If you set "v8_use_external_startup_data=true", don't forget to copy '*.bin' from both folders in "v8\out.gn\*.release" to the output folder for V8.Net for x64 and x86.

You should now be ready to build V8.NET! :)

Note: The proxy C++ projects expect the V8 source to be in "C:\ProgramData\Google\V8\src\" by default.  The "Common Properties" property page contains a "$(V8_SRC)" macro that must be updated to match where the source exists. The property pages are in "View->Other Windows->Property Manager" (expand the tree nodes). Open "Common Properties" and select "User Macros".

Optionally you can try the automated process by running V8Update.cmd - but Google likes to break things often so it may not always work.
   
Staying up to date: https://v8.dev/docs/source-code#staying-up-to-date

If you continue to have issues, there's a good post on it here that may also help:
https://medium.com/dailyjs/how-to-build-v8-on-windows-and-not-go-mad-6347c69aacd4
