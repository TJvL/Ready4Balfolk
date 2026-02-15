using System;
using System.Collections.Generic;
using Ready4Balfolk.Domain.Models.Tree;
using Ready4Balfolk.Domain.Services.Editor;
using Ready4Balfolk.Domain.Stores.Tree;

namespace Ready4Balfolk.UI.Views.DanceTree;

public record DanceTreeContext(
    Action<Func<IReadOnlyList<DanceBranch>, IReadOnlyList<DanceBranch>>> CommitDirect,
    Action<Func<IDanceTreeStore, DanceTreeAction>> CommitTracked,
    IObservable<IReadOnlyDictionary<string, int>> TrackCounts,
    IObservable<MarkedSelection> MarkedSelection,
    Action<MarkedSelection> SetMarked,
    Action<DanceCategoryNode> RequestAddBranch,
    Action<DanceCategoryNode> RequestAddLeaf,
    Action<object> ConfirmEdit,
    Action<object> CancelEdit,
    IReadOnlySet<string> CollapsedBranches);
