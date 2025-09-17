using FluentAssertions;
using AcadSync.Processor.Models.Projections;

namespace AcadSync.App.Tests
{
    [TestClass]
    public class PathResolverTests
    {
        [TestMethod]
        public void Resolve_Ext_Property_From_StudentProjection()
        {
            var student = new StudentProjection(
                id: 1,
                studentNumber: "S1",
                programCode: null,
                status: null,
                campus: null,
                citizenship: null,
                visaType: null,
                country: null,
                documents: new List<DocumentItem>(),
                ext: new Dictionary<string, string?> { ["InternationalFlag"] = "true" }
            );

            var result = student.ResolvePath("ext.InternationalFlag");
            result.Should().Be("true");
        }

        [TestMethod]
        public void Resolve_Document_Field_By_Type()
        {
            var doc = new DocumentItem("IMM", new Dictionary<string, object?> { ["ExpiryDate"] = "2024-09-01" });
            var student = new StudentProjection(
                id: 2,
                studentNumber: "S2",
                programCode: null,
                status: null,
                campus: null,
                citizenship: null,
                visaType: null,
                country: null,
                documents: new List<DocumentItem> { doc },
                ext: new Dictionary<string, string?>()
            );

            var result = student.ResolvePath("documents[docType=IMM].fields.ExpiryDate");
            result.Should().Be("2024-09-01");
        }
    }
}
