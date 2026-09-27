# TwinPix — developer guide

For a developer who knows PHP well and C# less: how this program is built, the
C# features it relies on (each with its PHP counterpart), how a desktop
application differs from a web request, and the safety rules every change must
keep. `README.md` describes what the program does; this file describes how the
code does it.

## 1. Building and running

| PHP | C# here |
| --- | --- |
| Each request interprets the scripts | `csc.exe` compiles every `.cs` file, once, into `TwinPix.exe` |
| `require` / autoloader | Nothing to include: all the files are compiled together, and every class sees every other one |
| `namespace App;` and `use Foo\Bar;` | `namespace TwinPix { ... }` and `using System.IO;` — the same ideas |
| PSR-4: one class per file | Same here: `Scanner` lives in `Scanner.cs`. Unlike PSR-4, C# does not tie namespaces to folders; this small program uses one namespace, `TwinPix`, and one folder |
| A syntax error shows at run time | A type or syntax error stops the build: nothing runs until everything compiles |

Three ways to build:

- `build.bat` — the compiler shipped with Windows, no install needed. This is
  the reference build.
- `TwinPix.csproj` in Visual Studio — needed for the form designer and the
  debugger (F5, breakpoints, step by step). A new `.cs` file must be added to
  the project (Solution Explorer, Add > Existing item); `build.bat` finds it on
  its own.
- `selftest.bat` — builds the sources with `tools\SelfTest.cs` and runs the
  safety self-test. Run it after any change to the engine.

### Only C# 5

The `csc.exe` shipped with Windows stops at C# 5 (2012). The project file sets
`<LangVersion>5</LangVersion>`, so Visual Studio flags the newer syntax as an
error too — code that builds in Visual Studio builds with `build.bat`. What you
will see in C# examples on the web, and what to write instead:

| Newer C# (not available) | C# 5 (write this) |
| --- | --- |
| `$"{count} files"` | `count + " files"` or `string.Format("{0} files", count)` |
| `a?.Name` | `a == null ? null : a.Name` |
| `x ?? throw new ...` | `if (x == null) throw new ...;` |
| `nameof(settings)` | `"settings"` |
| `int Count => _items.Count;` | `int Count { get { return _items.Count; } }` |
| `public int Max { get; } = 6;` | `public int Max { get; private set; }` + assign it in the constructor |
| `catch (IOException e) when (e.HResult == 5)` | `catch (IOException e) { if (e.HResult != 5) throw; ... }` |
| `int.TryParse(s, out var n)` | `int n; int.TryParse(s, out n)` |
| `(int, string) Pair()` (tuples) | a small class, or `out` parameters |
| `if (o is FileCard card)` | `var card = o as FileCard; if (card != null)` |
| `new Dictionary<string, int> { ["a"] = 1 }` | `new Dictionary<string, int> { { "a", 1 } }` |
| a function declared inside a method | a `private` method of the class |

Lambdas (`x => x * 2`), `var`, LINQ, generics, properties and `async`/`await`
are all C# 5 and fine to use.

## 2. The language, from PHP

### Types

C# checks every type when it compiles. `var` does not mean "any type": it means
"the type of the value on the right", fixed from then on.

| PHP | C# | Notes |
| --- | --- | --- |
| `int` (64 bits on 64-bit PHP) | `int` is **32 bits** (up to 2 GB), `long` is 64 | File sizes are always `long` here |
| — | `ulong` (unsigned 64 bits) | The image hashes |
| `float` | `double` | |
| `"5" + 1 === 6` | `"5" + 1 == "51"` | `+` on a string concatenates; no type juggling |
| `.` | `+` | String concatenation |
| `null` for anything | `null` only for classes and strings | `int`, `bool`, `DateTime` and structs can never be null |
| `$a ?? $b` | `a ?? b` | The same |
| — | Integer overflow wraps silently | `int.MaxValue + 1` is negative |

### Strings and paths

Strings cannot be modified: every "change" builds a new string (use a
`StringBuilder` in loops, as `Scanner.Md5` does). `==` compares the contents,
**with** case. Windows paths ignore case, so paths are always compared with
`StringComparison.OrdinalIgnoreCase` — or better, through `PathHelper`, which
also handles the trailing separator and the "C:\Photos2 is not inside
C:\Photos" trap. Never compare paths with `==` or `StartsWith` alone.

### Collections — and the difference that matters most

| PHP | C# |
| --- | --- |
| `[1, 2, 3]` | `int[]` (fixed size) or `List<int>` (grows) |
| `['key' => 'value']` | `Dictionary<string, string>` — `TryGetValue` instead of `isset` |
| `array_flip` + `isset` | `HashSet<string>` |
| `array_map`, `array_filter`, `usort` | LINQ `Select`, `Where`, `OrderBy`, or `List.Sort(comparison)` |

**A PHP array is copied when you assign it or pass it to a function. A C#
`List` or `Dictionary` is not: every variable holding it points to the same
object**, like a PHP object. When `FinishRemoval` removes a file from
`item.Group.Files`, the group shown in the window loses it too — it is the same
list. The exception is a `struct` (`FileIdentity`, `DateTime`, `int`): those
are copied, like PHP arrays.

### Classes

- Members without a keyword are **private** (in PHP they are public). This code
  writes `private` explicitly anyway.
- `public string Name { get; set; }` is a property: it reads like a public field,
  but can later get a getter or setter body without the callers changing.
  `{ get; private set; }` — only the class itself may assign it.
- The constructor has the class's name (PHP `__construct`).
- `static class` — only static members, never instantiated (`PathHelper`,
  `Scanner`).
- `readonly` — assigned once, in the constructor (PHP 8.1 `readonly`).
  `const` — a compile-time constant.
- `sealed` — cannot be inherited (PHP `final`).
- `partial class` — one class written in several files. `MainForm.cs` holds the
  behaviour, `MainForm.Designer.cs` the controls; the compiler joins them.
- `enum` — a closed set of named values (PHP 8.1 enums): `MatchMode`,
  `KeepRule`, `RemovalOutcome`.
- Interfaces: `IDisposable` (has a `Dispose()`), `IComparer` (a comparison, like
  the callback of `usort`).

### Parameters

`ref` and `out` pass a variable by reference, like PHP's `&$x`; `out` also means
"the method must assign it" — a way to return several values
(`ImageHash.Decode` returns the pixels and gives back width, height and frame
count through `out`).

### Exceptions

`try` / `catch` / `finally` work as in PHP. Two habits of this code:

- **Catch only what you expect.** A missing file, an access denied or a file in
  use are normal events; a `NullReferenceException` is a bug and must not be
  swallowed. Since C# 5 has no exception filters, the pattern is:

  ```csharp
  catch (Exception ex)
  {
      if (!PathHelper.IsFileSystemError(ex)) throw;   // a bug: let it surface
      result.Errors++;                                // an ordinary failure
  }
  ```

  `throw;` alone re-throws the exception being handled, keeping its stack
  trace.
- Where a catch-all `catch { }` remains, a comment says why: the cosmetic Win32
  helpers of `Native`, and the image decoders — GDI+ reports an unknown image
  format as `OutOfMemoryException`, WIC as a COM error.

### Closing what you open

PHP frees an object as soon as nothing refers to it. .NET frees memory later,
when its garbage collector decides to — so objects holding a Windows resource
(an open file, a bitmap, a font) are closed explicitly with `Dispose()`, or with
a `using` block, which calls `Dispose()` at its closing brace even if an
exception is thrown:

```csharp
using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
{
    ...                         // fs is closed here, whatever happens
}
```

This matters for safety: a file left open by a forgotten thumbnail could not be
moved. `Thumbnail.Load` decodes into memory and closes the file at once.

### Lambdas, closures and events

| PHP | C# |
| --- | --- |
| `fn($x) => $x * 2` | `x => x * 2` |
| `function ($a, $b) use ($c) { ... }` | `(a, b) => { ... }` — outer variables are used directly |
| `set_exception_handler($f)`, callbacks | events: `button.Click += Handler;` |

One trap: **a C# lambda captures the variable itself, not its value** (PHP's
`use` copies the value). A lambda created in a loop and run later sees the
variable's last value — which is why `MainForm.OnLoad` copies the loop variable
into a fresh `target` variable before using it in a lambda.

Events are lists of callbacks. `_btnScan.Click += BtnScan_Click;` (in the
designer file) registers `BtnScan_Click(object sender, EventArgs e)`, which
Windows then calls on every click. `sender` is the control that raised it.

### LINQ

`_groups.Where(g => g.Visual).Select(g => g.Files.Count).ToList()` is
`array_map` over `array_filter`, written left to right. `Any`, `First`,
`OrderByDescending`, `Take` and `Sum` do what their names say. Without
`ToList()` the query is lazy: it runs when it is enumerated.

### Attributes and conditional compilation

`[STAThread]` or `[DllImport("kernel32.dll")]` are attributes, the same idea as
PHP 8's `#[...]`: metadata the compiler or the runtime reads. `#if WIC ...
#endif` keeps or drops code at build time, depending on whether `/define:WIC`
was given (it enables the fast WIC image decoder).

### Culture

`ToString()` and `Parse` follow the user's regional settings: a Belgian
Windows writes `1,5` where an English one writes `1.5`. Anything written to a
file (CSV, journal) uses `CultureInfo.InvariantCulture`; anything shown on
screen uses the user's culture.

## 3. A desktop program is not a request

A PHP request starts, runs and ends. `TwinPix.exe` starts, opens its window and
then **waits**: Windows calls its methods when something happens — a click, a
key, a resize — until the window closes (`Application.Run` in `Program.cs`).

- **The window belongs to one thread** (the UI thread). Only that thread may
  touch a control, and while it works the window cannot repaint or react: it
  "freezes". So anything long runs elsewhere.
- **BackgroundWorker** is how this code runs long work (a scan, a batch of
  moves): `DoWork` runs on a background thread and must not touch any control;
  `ProgressChanged` and `RunWorkerCompleted` are called back on the UI thread,
  where the window can be updated. See `StartScan` and `RemoveDuplicates` in
  `MainForm.cs`.
- **Several threads at once**: the fingerprints are computed on every core
  (`Parallel.For` in `Scanner.ComputeFingerprints`). Data shared between those
  threads is protected with `lock` (a mutex, `FingerprintCache`) or updated with
  `Interlocked.Increment` (an atomic `count++`).
- **The form designer** owns the `.Designer.cs` files. Rules kept by this
  project: no control is created outside them; event handlers are named methods
  (the designer cannot read lambdas); anything machine-dependent is set in the
  constructor; `AutoScaleMode = None` on both forms.
- **Screen scaling**: the program is not "DPI-aware" (Windows enlarges it on a
  150 % screen). The WPF classes used for decoding would switch that on in the
  middle of a scan; the attribute at the top of `TwinPix.cs` prevents it.

## 4. Calling Windows directly

`NativeMethods.cs` declares every Windows function the program calls, like PHP's
FFI: `[DllImport("shell32.dll")] static extern int SHFileOperationW(...)`.
The structures passed to them must match the C declarations byte for byte:
same fields, same order, same sizes, same packing — or Windows reads the wrong
memory. `SHFILEOPSTRUCT` is even declared twice, because Windows packs it
differently in 32-bit and 64-bit programs. Strings are `CharSet.Unicode`.

Nothing else calls `NativeMethods` directly: each call is wrapped in a method
that says what it is for (`Native.SetSortArrow`, `RecycleBin.Send`,
`FileIdentity.Of`, `FolderWalker.IsLink`) and that does nothing when not on
Windows.

## 5. Safety rules — keep them in every change

These rules are why TwinPix can be trusted with a photo library. Each one
answers a way in which duplicate finders have destroyed files; `selftest.bat`
checks them.

1. **Never remove a file on the strength of the scan alone.** Every removal goes
   through `DuplicateRemover`, whose `WhyNotRemovable` re-checks the file
   against the kept copy at the last moment. No other code moves, recycles or
   deletes a picture.
2. **Never overwrite, never delete.** Files move with `File.Move` to a name from
   `PathHelper.FreeFileName`; `File.Move` refuses to replace an existing file.
   No `File.Delete` on a picture, no `File.Copy(..., true)`.
3. **The Recycle Bin only where it exists.** `RecycleBin.WhyNotAvailable` before
   `RecycleBin.Send`, always: elsewhere Windows deletes for good, silently.
4. **One file, one name.** Links and junctions are not followed
   (`FolderWalker`), and two names of one file are never treated as two copies
   (`FileIdentity`).
5. **Never scan what must not be kept**: the destination folder, the Recycle
   Bin, the system folders.
6. **One kept copy per group, fixed before anything moves.**
   `DuplicateRemover.Plan` takes a snapshot of the pairs when the user confirms;
   the window stays locked until the moves are over.
7. **Journal first.** If `RemovalJournal.Open` fails, nothing moves.
8. **Full paths only, compared with `PathHelper`.** Two traps: `Path.Combine(a, b)`
   returns `b` when `b` is itself a full path, and `Path.GetFullPath("C:")` is
   the current folder of drive C, not its root.
9. **Risky confirmations default to Cancel** (`Native.ConfirmRisky`).
10. **A new rule gets a self-test check** in `tools\SelfTest.cs`.

## 6. Where things are stored

| File | Contents |
| --- | --- |
| `%APPDATA%\TwinPix\folders.txt` | remembered folders and the window placement |
| `%APPDATA%\TwinPix\fingerprints.bin` | the fingerprint cache (safe to delete) |
| `%APPDATA%\TwinPix\startup-error.txt` | unexpected errors, with their stack trace |
| `<destination>\TwinPix-journal.csv` | every file moved to that folder |
| `%APPDATA%\TwinPix\TwinPix-journal.csv` | every file sent to the Recycle Bin |

## 7. Common changes

- **A new keep rule**: a value in `KeepRule`, its comparison in
  `KeepSelector.IsBetter`, a check box in the Keep bar (designer) with a handler
  calling `RuleChanged`, and a line in `CurrentRule`, `RuleChecks` and
  `SyncRuleMenu`.
- **A new scan option**: a property in `ScanOptions`, read in
  `MainForm.ReadScanOptions`, used in `FolderWalker` or `Scanner`; a control in
  the designer whose change handler calls `ScanOptionChanged`.
- **A new file-type rule for visual matching**: `VisualMatcher.SamePicture`
  decides both grouping and the last check before removal — change it there,
  once.
- **Anything that removes files**: don't. Extend `DuplicateRemover` instead, and
  add a check to the self-test.
