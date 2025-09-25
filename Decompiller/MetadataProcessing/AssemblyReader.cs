using Decompiller.Extentions;
using Decompiller.Providers;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace Decompiller.MetadataProcessing
{
    public class AssemblyReader
    {
        private readonly MetadataReader _reader;
        private readonly PEReader _peReader;

        public TypeDefinitionHandleCollection TypeDefinitions => _reader.TypeDefinitions;
        public PEReader PEReader => _peReader;
        public MetadataReader Reader => _reader;

        public AssemblyReader(string filePath)
        {
            var stream = File.OpenRead(filePath);
            var peReader = new PEReader(stream);
            var reader = peReader.GetMetadataReader();
            _reader = reader;
            _peReader = peReader;
        }

        public TypeDefinition GetTypeDefinition(TypeDefinitionHandle handle)
        {
            return _reader.GetTypeDefinition(handle);
        }

        public MethodDefinition GetMethodDefinition(MethodDefinitionHandle handle)
        {
            return _reader.GetMethodDefinition(handle);
        }

        public string GetString(StringHandle handle)
        {
            return _reader.GetString(handle).SanitizeName();
        }

        public string GetUserString(UserStringHandle handle)
        {
            return _reader.GetUserString(handle);
        }

        public string DecodeFieldSignature(ref BlobReader sigReader, LocalTypeProvider typeProvider)
        {
            return new SignatureDecoder<string, object>(typeProvider, _reader, new object()).DecodeFieldSignature(ref sigReader).SanitizeName();
        }
    }
}
