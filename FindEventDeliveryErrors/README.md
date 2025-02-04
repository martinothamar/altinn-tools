### Program for finding event delivery errors

You need 

* Azure CLI
* Login with ai-dev prod account which has access to prod log analytics workspaces for service owners


```
dotnet run -c Release
```


These parameters can be tweaked in Program.cs:

```csharp
int DaysToSearch = 1;
TimeSpan PollInterval = TimeSpan.FromMinutes(1);
```

The `data/` folder in should have telemetry data. 
