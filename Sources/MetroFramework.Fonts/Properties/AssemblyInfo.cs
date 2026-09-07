using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

// JT: Ensure API compatibility
[assembly: CLSCompliant(true)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("639b45e3-11f9-4e52-8479-7b6f20a5ce98")]

// Test assembly. Signed with the same key so the friend declaration resolves
// for a strong-named assembly.
[assembly: InternalsVisibleTo(MetroFramework.AssemblyRef.MetroFrameworkFontsTestsIVT)]
