# Visual Studio integration for LLDB

This code integrates LLDB remote debugging into Microsoft Visual Studio. This
extension is developed and maintained by the Stadia team.

## Prerequisites

1. Initialize the required submodules:

   ```bash
   git submodule init
   git submodule update
   ```

1. Download and install the latest Stadia SDK from the Stadia Partner Resource
   Center.

1. Make sure the following tools are installed on your system (minimal
   recommended version is specified in parenthesis):

   * CMake (>= 3.16.0)
   * Ninja (>= 1.8.2)
   * Python 3 (>= 3.7)
   * SWIG (>= 4.0.2)
   * Visual Studio 2017 (>= 15.9.15) or Visual Studio 2019 (>= 16.6.3).

## Build Clang and LLDB

1. (Optional) Apply the recommended patches:

   ```bash
   cd llvm-project
   git apply ../patches/llvm-project/*.patch  # Use "git am" to import as commits
   ```

   These patches are not required, but they fix some bugs and performance
   issues.

1. Open the `x64 Native Tools Command Prompt for VS 2017` (or 2019 if you plan
   to use only VS 2019).

1. Create a build directory inside `llvm-project`. Assuming your `vsi-lldb`
   project is located in `C:\Projects`:

   ```bash
   cd C:\Projects\vsi-lldb\llvm-project
   mkdir build_x64_optdebug
   cd build_x64_optdebug
   ```

1. Run the following command (modify `CMAKE_INSTALL_PREFIX`, `PYTHON_HOME`,
   `SWIG_DIR` and `SWIG_EXECUTABLE` accordingly):

   ```bash
   cmake ^
     -DCMAKE_INSTALL_PREFIX='.' ^
     -DLLVM_ENABLE_PROJECTS='lldb;clang;lld' ^
     -DPYTHON_HOME=%LOCALAPPDATA%\Programs\Python\Python37 ^
     -DSWIG_DIR=C:\Swig ^
     -DSWIG_EXECUTABLE=C:\Swig\swig.exe ^
     -DLLDB_ENABLE_PYTHON=1 ^
     -DCMAKE_BUILD_TYPE=RelWithDebInfo ^
     -DLLVM_USE_CRT_RELWITHDEBINFO=MT ^
     -GNinja ^
     ../llvm
   ```

1. Run the build:

   ```bash
   ninja install
   ```

## Build & Run vsi-lldb

1. Open `Build.props` file located in the root of the project and make sure
   `PythonDir` is pointing to the Python used to build LLDB and `PythonVersion`
   is correct.
1. Import `vsi-lldb.sln` in your Visual Studio.
1. Choose `Debug2017` or `Debug2019` as a Solution Configuration depending on
   your VS version.
1. Make sure `YetiVSI` is selected as a startup project.
1. Build & Run the solution.

Note: if you are using the `Stadia for Visual Studio` extension distributed with
the Stadia SDK, make sure to disable or uninstall it before debugging with the
open-source version of the extension. Only one of these extensions should be
active at each moment in order for the debugger to work.

## Troubleshooting

### Attaching to a remote process is slow

Upon the attach to a remote process the debugger may need to download some data
(e.g. process binary, shared libraries, debug symbols). We recommend applying
the following patch to improve the download speed --
`patches/llvm-project/0001-increase-buffer-size.patch`.

## Disclaimer

This is not an officially supported Google product.
