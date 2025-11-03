# WorldToVMaNGOS
**Convert legacy WoWEmu `world.save` files to VMaNGOS SQL**

## How to build

```bash
# Linux x64 (self-contained, single-file)
dotnet publish WorldToVMaNGOS/WorldToVMaNGOS.csproj -c Release -r linux-x64 -o publish/linux-x64

# Windows x64 (self-contained, single-file)
dotnet publish WorldToVMaNGOS/WorldToVMaNGOS.csproj -c Release -r win-x64 -o publish/win-x64
```

## How to run
```bash
# Linux
./WorldToVMaNGOS path/to/world.save

# Windows
WorldToVMaNGOS.exe "C:\path\to\world.save"
```
