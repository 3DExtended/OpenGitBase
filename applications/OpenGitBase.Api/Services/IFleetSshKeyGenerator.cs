namespace OpenGitBase.Api.Services;

public interface IFleetSshKeyGenerator
{
    (string PublicKey, string PrivateKey) GenerateEd25519KeyPair();
}
