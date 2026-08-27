<!--
The title becomes the squash commit subject, so make it the real one: conventional commit style,
with a scope where there is an obvious one.

Label the pull request. Release notes are generated from labels, and an unlabelled one lands in
"Other changes".
-->

## What and why

<!-- What changed, and the reason. The diff already says what; say why. -->

Closes #

## Verified

<!--
What you actually ran, not what you assume CI will do. For a bug fix, say whether the new test
fails without the change, and say so plainly if it does not.
-->

- [ ] `dotnet build Ready4Balfolk.sln -c Release`
- [ ] `dotnet format Ready4Balfolk.sln --verify-no-changes`
- [ ] `dotnet test --project Ready4Balfolk.Tests/Ready4Balfolk.Tests.csproj -c Release`
- [ ] New or changed user-facing strings exist in both `.resx` files

## Worth a second look

<!-- Anything you are unsure about, or a judgement call somebody might reasonably disagree with. -->
