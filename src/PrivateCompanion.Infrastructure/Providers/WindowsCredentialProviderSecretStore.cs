using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using PrivateCompanion.Application;

namespace PrivateCompanion.Infrastructure.Providers;

public sealed class WindowsCredentialProviderSecretStore : IProviderSecretStore
{
    private const int CredentialTypeGeneric = 1;
    private const int CredentialPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;

    public Task<bool> ExistsAsync(Guid providerId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows()) return Task.FromResult(false);
        var found = CredRead(Target(providerId), CredentialTypeGeneric, 0, out var pointer);
        if (found) CredFree(pointer);
        else if (Marshal.GetLastWin32Error() != ErrorNotFound) ThrowLastWin32();
        return Task.FromResult(found);
    }

    public Task<string?> GetAsync(Guid providerId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureWindows();
        if (!CredRead(Target(providerId), CredentialTypeGeneric, 0, out var pointer))
        {
            if (Marshal.GetLastWin32Error() == ErrorNotFound) return Task.FromResult<string?>(null);
            ThrowLastWin32();
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(pointer);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0)
                return Task.FromResult<string?>(null);
            var bytes = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
            return Task.FromResult<string?>(Encoding.Unicode.GetString(bytes));
        }
        finally
        {
            CredFree(pointer);
        }
    }

    public Task SaveAsync(Guid providerId, string secret, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureWindows();
        if (string.IsNullOrWhiteSpace(secret)) return DeleteAsync(providerId, cancellationToken);

        var bytes = Encoding.Unicode.GetBytes(secret.Trim());
        var blob = Marshal.AllocCoTaskMem(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var credential = new NativeCredential
            {
                Type = CredentialTypeGeneric,
                TargetName = Target(providerId),
                CredentialBlobSize = bytes.Length,
                CredentialBlob = blob,
                Persist = CredentialPersistLocalMachine,
                UserName = "Tinta Viva"
            };
            if (!CredWrite(ref credential, 0)) ThrowLastWin32();
            return Task.CompletedTask;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            Marshal.FreeCoTaskMem(blob);
        }
    }

    public Task DeleteAsync(Guid providerId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureWindows();
        if (!CredDelete(Target(providerId), CredentialTypeGeneric, 0) && Marshal.GetLastWin32Error() != ErrorNotFound)
            ThrowLastWin32();
        return Task.CompletedTask;
    }

    private static string Target(Guid providerId) => $"TintaViva/Provider/{providerId:N}";

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("El almacenamiento protegido de tokens requiere Windows.");
    }

    private static void ThrowLastWin32() => throw new Win32Exception(Marshal.GetLastWin32Error());

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public int Flags;
        public int Type;
        public string TargetName;
        public string? Comment;
        public long LastWritten;
        public int CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite([In] ref NativeCredential credential, int flags);

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, int type, int flags, out IntPtr credential);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, int type, int flags);

    [DllImport("advapi32.dll", SetLastError = false)]
    private static extern void CredFree(IntPtr buffer);
}
