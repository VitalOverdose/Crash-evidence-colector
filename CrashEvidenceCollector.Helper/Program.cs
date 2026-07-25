using CrashEvidenceCollector.Core;

// This process is manifested requireAdministrator. It accepts one allow-listed,
// read-only source operation over a nonce-authenticated, current-user named pipe.
return await ElevatedHelperIpc.RunHelperAsync(args, CancellationToken.None);
