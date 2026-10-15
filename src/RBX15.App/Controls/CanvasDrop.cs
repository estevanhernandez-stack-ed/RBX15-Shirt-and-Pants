using System.Windows;

namespace RBX15.App.Controls;

/// <summary>
/// Files dropped on the canvas (drop-canvas): the paths in the order the drag source lists them,
/// and the drop point in template pixels (editor.js:966-967). Every file lands on that one point,
/// as 4.1's <c>handleFiles(e.dataTransfer.files, dropX, dropY)</c> does (editor.js:984); Task 4's
/// ImportService places them.
/// </summary>
public sealed record CanvasDrop(IReadOnlyList<string> Paths, Point TemplatePoint);
