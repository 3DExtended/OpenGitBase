using System.Buffers.Binary;
using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpenGitBase.Common.Tests.Testing;

/// <summary>
/// A passkey in memory: one ES256 key pair with attestation <c>none</c>. It answers WebAuthn creation
/// and request options the way a browser's <c>PublicKeyCredential.toJSON()</c> does, so tests run the
/// real Fido2NetLib verification. The settable properties produce the broken answers the server must
/// reject.
/// </summary>
public sealed class SoftwarePasskeyAuthenticator : IDisposable
{
    private const byte UserPresent = 0x01;
    private const byte UserVerifiedFlag = 0x04;
    private const byte BackupEligible = 0x08;
    private const byte BackedUp = 0x10;
    private const byte AttestedCredentialData = 0x40;

    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private byte[] _userHandle = [];

    public SoftwarePasskeyAuthenticator(string origin)
    {
        Origin = origin;
    }

    public byte[] CredentialId { get; } = RandomNumberGenerator.GetBytes(16);

    /// <summary>The origin written into <c>clientDataJSON</c>.</summary>
    public string Origin { get; set; }

    /// <summary>Signs for this RP ID instead of the one in the options.</summary>
    public string? RelyingPartyIdOverride { get; set; }

    public bool UserVerified { get; set; } = true;

    /// <summary>Reports the credential as synced, as iCloud Keychain or Google Password Manager do.</summary>
    public bool Synced { get; set; }

    public uint SignCount { get; private set; }

    public static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] FromBase64Url(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '='));
    }

    /// <summary>Answers <c>navigator.credentials.create()</c> for the given creation options.</summary>
    public string CreateCredential(string creationOptionsJson)
    {
        var options = JsonNode.Parse(creationOptionsJson)!;
        _userHandle = FromBase64Url(options["user"]!["id"]!.GetValue<string>());
        var relyingPartyId = RelyingPartyIdOverride ?? options["rp"]!["id"]!.GetValue<string>();
        var clientData = ClientData("webauthn.create", options["challenge"]!.GetValue<string>());

        var credentialData = new List<byte>();
        credentialData.AddRange(new byte[16]); // AAGUID: none
        var length = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(length, (ushort)CredentialId.Length);
        credentialData.AddRange(length);
        credentialData.AddRange(CredentialId);
        credentialData.AddRange(CosePublicKey());
        var authenticatorData = AuthenticatorData(relyingPartyId, AttestedCredentialData, [.. credentialData]);

        var attestation = new CborWriter(CborConformanceMode.Lax);
        attestation.WriteStartMap(3);
        attestation.WriteTextString("fmt");
        attestation.WriteTextString("none");
        attestation.WriteTextString("attStmt");
        attestation.WriteStartMap(0);
        attestation.WriteEndMap();
        attestation.WriteTextString("authData");
        attestation.WriteByteString(authenticatorData);
        attestation.WriteEndMap();

        return Credential(
            new JsonObject
            {
                ["clientDataJSON"] = ToBase64Url(clientData),
                ["attestationObject"] = ToBase64Url(attestation.Encode()),
                ["transports"] = new JsonArray("internal", "hybrid"),
            }
        );
    }

    /// <summary>Answers <c>navigator.credentials.get()</c> for the given request options.</summary>
    public string GetAssertion(string requestOptionsJson)
    {
        var options = JsonNode.Parse(requestOptionsJson)!;
        var relyingPartyId = RelyingPartyIdOverride ?? options["rpId"]!.GetValue<string>();
        var clientData = ClientData("webauthn.get", options["challenge"]!.GetValue<string>());
        SignCount++;
        var authenticatorData = AuthenticatorData(relyingPartyId, 0, []);
        var signature = _key.SignData(
            [.. authenticatorData, .. SHA256.HashData(clientData)],
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence
        );

        return Credential(
            new JsonObject
            {
                ["clientDataJSON"] = ToBase64Url(clientData),
                ["authenticatorData"] = ToBase64Url(authenticatorData),
                ["signature"] = ToBase64Url(signature),
                ["userHandle"] = ToBase64Url(_userHandle),
            }
        );
    }

    public void Dispose() => _key.Dispose();

    private string Credential(JsonObject response) =>
        new JsonObject
        {
            ["id"] = ToBase64Url(CredentialId),
            ["rawId"] = ToBase64Url(CredentialId),
            ["type"] = "public-key",
            ["response"] = response,
            ["clientExtensionResults"] = new JsonObject(),
            ["authenticatorAttachment"] = "platform",
        }.ToJsonString();

    private byte[] ClientData(string type, string challenge) =>
        Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(
                new Dictionary<string, object>
                {
                    ["type"] = type,
                    ["challenge"] = challenge,
                    ["origin"] = Origin,
                    ["crossOrigin"] = false,
                }
            )
        );

    private byte[] AuthenticatorData(string relyingPartyId, byte extraFlags, byte[] attestedCredentialData)
    {
        var flags = (byte)(UserPresent | extraFlags);
        if (UserVerified)
        {
            flags |= UserVerifiedFlag;
        }

        if (Synced)
        {
            flags |= BackupEligible | BackedUp;
        }

        var counter = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(counter, SignCount);
        return [.. SHA256.HashData(Encoding.UTF8.GetBytes(relyingPartyId)), flags, .. counter, .. attestedCredentialData];
    }

    /// <summary>The public key as a COSE EC2 map, keys in canonical order.</summary>
    private byte[] CosePublicKey()
    {
        var parameters = _key.ExportParameters(false);
        var writer = new CborWriter(CborConformanceMode.Ctap2Canonical);
        writer.WriteStartMap(5);
        writer.WriteInt32(1); // kty: EC2
        writer.WriteInt32(2);
        writer.WriteInt32(3); // alg: ES256
        writer.WriteInt32(-7);
        writer.WriteInt32(-1); // crv: P-256
        writer.WriteInt32(1);
        writer.WriteInt32(-2); // x
        writer.WriteByteString(parameters.Q.X!);
        writer.WriteInt32(-3); // y
        writer.WriteByteString(parameters.Q.Y!);
        writer.WriteEndMap();
        return writer.Encode();
    }
}
