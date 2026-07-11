# Aetheria Unity Client

This is Aetheria's Unity client and the canonical configuration example for
lowering its advertised 3D pilot surface through EveUnity. The client contains no
Aetheria runtime code. Aetheria owns the daemon surface and native asset bundle;
EveUnity owns discovery, transport, lowering, input, camera, and presentation.

From the Aetheria repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run-aetheria-unity.ps1
```

The launcher builds the provider's Unity AssetBundle, starts the Aetheria daemon,
builds this project, and opens the standalone client. Close the client window to
stop the daemon.

Configuration is supplied through `EVEUNITY_RENDEZVOUS_ENDPOINT` and
`EVEUNITY_SURFACE_ID`. The defaults are `rudp://127.0.0.1:3076` and
`aetheria.pilot`.
