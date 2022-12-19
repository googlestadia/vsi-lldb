using System.IO;
using NUnit.Framework;

namespace YetiCommon.Tests
{
    public partial class ModuleParserTests
    {
        [TestCase("hello_cpp_exe.dat", ModuleFormat.Pe,
                  "DD2CC932-5E32-4D66-8B03-875CC0965322-00000001")]
        [TestCase("hello_cpp_pdb.dat", ModuleFormat.Pdb,
                  "DD2CC932-5E32-4D66-8B03-875CC0965322-00000001")]
        [TestCase("hello_elf.dat", ModuleFormat.Elf,
                  "7119D4F0-85EB-2938-0EAF-23B40BA54FC1-73BF3747")]
        [TestCase("hello_dotnet_exe.dat", ModuleFormat.Pe,
                  "445A5B1C-1EF1-4B03-8193-7A4B770873AB-00000001")]
        [TestCase("hello_dotnet_pdb.dat", ModuleFormat.Pdb,
                  "FDF485D6-E134-4825-885F-03517602265D-00000001")]
        [TestCase("hello_dotnet_dll.dat", ModuleFormat.Pe,
                  "FDF485D6-E134-4825-885F-03517602265D-00000001")]
        public void ParseBuildId_Succeeds(string filename, ModuleFormat format, string buildId)
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData",
                                       "ModuleParserTests", filename);
            var moduleParser = new ModuleParser();
            BuildIdInfo output = moduleParser.ParseBuildIdInfo(path, format);

            Assert.IsFalse(output.HasError);
            Assert.True(output.Data.Matches(new BuildId(buildId), format));
        }

        [Test]
        public void ParseBuildId_FileNotFound([Values] ModuleFormat format)
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData",
                                       "ModuleParserTests", "unknown_file");
            var moduleParser = new ModuleParser();
            BuildIdInfo output = moduleParser.ParseBuildIdInfo(path, format);

            Assert.That(output.Error.EndsWith("unknown_file not found"));
        }

        [TestCase("hello_cpp_exe.dat", ModuleFormat.Elf)]
        [TestCase("hello_cpp_exe.dat", ModuleFormat.Pdb)]
        [TestCase("hello_cpp_pdb.dat", ModuleFormat.Elf)]
        [TestCase("hello_cpp_pdb.dat", ModuleFormat.Pe)]
        [TestCase("hello_elf.dat", ModuleFormat.Pe)]
        [TestCase("hello_elf.dat", ModuleFormat.Pdb)]
        [TestCase("hello_dotnet_dll.dat", ModuleFormat.Elf)]
        [TestCase("hello_dotnet_dll.dat", ModuleFormat.Pdb)]
        public void ParseBuildId_IncorrectFormat(string filename, ModuleFormat format)
        {
            string path = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestData",
                                       "ModuleParserTests", filename);
            var moduleParser = new ModuleParser();
            BuildIdInfo output = moduleParser.ParseBuildIdInfo(path, format);

            Assert.IsTrue(output.HasError);
            Assert.That(output.Error.Equals(ErrorStrings.InvalidSymbolFileFormat(path, format)));
        }
    }
}