# GetHashes
A Windows tool to get several hashes of a file in argument.

Prerequisite: .NET 4.5.2

The tool can be used in 2 modes:
- by using cmd.exe and putting the file in argument of GetHashes.exe
- by adding a GetHashes menu when a right click is performed on a file. The setup is in the registry:
```
HKEY_CLASSE_ROOT\*\Shell\GetHashes\command -> Default value: c:\path\GetHashes.exe "%1"
```

Screenshot:

![alt tag](https://github.com/SekoiaLab/GetHashes/raw/master/Binary_v1.0.0.0/Capture.PNG)
