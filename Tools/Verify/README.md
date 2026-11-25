# Scenario smoke test (batch mode)

Plays the whole patient-room scenario without a headset and saves screenshots of the room, highlights, PDA screens and wrist checklist.

1. Copy the project somewhere else (Unity can't open the same project twice).
2. Put `VerifyDriver.cs` under `Assets/HSE/Scripts/_Verify/` and `VerifyRunner.cs` under `Assets/HSE/Editor/_Verify/` in the copy.
3. Run: `Unity.exe -batchmode -projectPath <copy> -executeMethod VerifyRunner.Run -logFile verify.log`

Results go to `<copy>/../verify_out` (`report.txt` + PNGs). The exit code is 0 when every check passes.
