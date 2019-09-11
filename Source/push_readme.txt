Use PushPackage.cmd to push a new release to NuGet. You will need:

1. To make sure the version was updated on a release build first!!!
2. You added release notes!!!
3. The NuGet key.  The script will ask for it.
4. The version number you just set: The script will ask for that also.

When you run the command script, it will prompt for a NuGet key, then list all the available packages you can send. You would
then enter the version number, such as '1.0.0', then the associated package will be uploaded.

The GitHub page is using 'img.shields.io', which will auto-update to the correct NuGet version and link some time after upload (it is not immediate).
