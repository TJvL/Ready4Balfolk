namespace Ready4Balfolk.Domain.Services.Tagging;

/// <summary>Why a value was not recognised, which decides what can be done about it.</summary>
/// <remarks>
/// The distinction between the first two is the whole point. They look alike in a list and need
/// opposite treatment: one is a single decision, the other cannot be one.
/// </remarks>
public enum UnrecognisedKind
{
    /// <summary>
    /// Close to exactly one name in the list. "Hanterdro" means "Hanter dro" in all 34 files, so one
    /// decision settles every one of them.
    /// </summary>
    Misspelled,

    /// <summary>
    /// Sits inside several names in the list. "Bourrée" across 50 tracks is some 2 temps, some
    /// 3 temps and some Auvergnate, so mapping the value would invent 50 confident answers. It gets
    /// no map at all, and is split by folder instead.
    /// </summary>
    TooGeneral,

    /// <summary>Nothing in the list is near it. It needs a person, or the list needs a new dance.</summary>
    Unknown,

    /// <summary>
    /// A dance the list already knows. These tracks named it and something else as well, so nothing
    /// was assumed. "Mazurka-valse" is a mazurka and a valse, and which one it is played as is a
    /// decision about that track rather than about the word.
    /// </summary>
    Ambiguous
}
