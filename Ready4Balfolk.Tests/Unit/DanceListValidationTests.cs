using Ready4Balfolk.Domain.Models.Dances;
using Ready4Balfolk.Domain.Services.Dances;
using Ready4Balfolk.Tests.Helpers;

namespace Ready4Balfolk.Tests.Unit;

public sealed class DanceListValidationTests
{
    [Fact]
    public void Validate_HealthyList_HasNoProblems()
    {
        var problems = DanceListValidation.Validate(TestData.CreateSimpleDanceList());

        Assert.False(problems.Any);
    }

    [Fact]
    public void Validate_EmptyList_HasNoProblems()
    {
        var problems = DanceListValidation.Validate(DanceList.Empty);

        Assert.False(problems.Any);
    }

    [Fact]
    public void Validate_NameSharedByTwoDances_IsReported()
    {
        var list = ListOf(
            TestData.CreateDance("hanter-dro", names: ["Hanter dro"]),
            TestData.CreateDance("andro", names: ["Andro", "Hanter-dro"]));

        var problems = DanceListValidation.Validate(list);

        // "Hanter-dro" and "Hanter dro" fold to the same string, so they are the same name as far as
        // anything downstream can tell.
        Assert.Contains("Hanter-dro", problems.DuplicateNames);
    }

    [Fact]
    public void Validate_NameRepeatedWithinOneDance_IsNotADuplicate()
    {
        var list = ListOf(TestData.CreateDance("andro", names: ["Andro", "andro"]));

        var problems = DanceListValidation.Validate(list);

        Assert.Empty(problems.DuplicateNames);
    }

    [Fact]
    public void Validate_SlugUsedTwice_IsReported()
    {
        var list = ListOf(
            TestData.CreateDance("andro", names: ["Andro"]),
            TestData.CreateDance("andro", names: ["An dro"]));

        var problems = DanceListValidation.Validate(list);

        Assert.Contains("andro", problems.DuplicateSlugs);
    }

    [Fact]
    public void Validate_DanceWithNoUsableName_IsReported()
    {
        var list = ListOf(new Dance { Slug = "nameless", Names = ["  "] });

        var problems = DanceListValidation.Validate(list);

        Assert.Contains("nameless", problems.SlugsWithoutNames);
    }

    [Fact]
    public void Validate_TagNotDeclaredAtTheTop_IsReported()
    {
        var list = new DanceList
        {
            Tags = ["bretagne"],
            Dances = [TestData.CreateDance("andro", ["bretagne", "invented"], "Andro")]
        };

        var problems = DanceListValidation.Validate(list);

        // Every tag a dance carries has to appear in the list's own tag array; BigBalfolkList's
        // build enforces it, so a file arriving without it has been edited by hand.
        Assert.Contains("invented", problems.UndeclaredTags);
    }

    private static DanceList ListOf(params Dance[] dances) => new() { Dances = dances };
}
