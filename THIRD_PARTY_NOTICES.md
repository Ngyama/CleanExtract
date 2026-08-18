# Third-party notices

Clean Extract does not implement ZIP, RAR, or 7z codecs. Archive listing,
content inspection, and extraction are performed by the official 7-Zip console
tools that are bundled with this application.

## 7-Zip

This product includes 7-Zip, Copyright (C) 1999-2026 Igor Pavlov.

Bundled files (from the official 7-Zip 26.02 Windows x64 distribution):

- `resources/7zz.exe` — copy of `7z.exe`, renamed to match the standalone console name used in this project
- `resources/7z.dll` — required 7-Zip library (must stay next to `7zz.exe`)
- `resources/7-Zip-License.txt` — original 7-Zip license text

7-Zip website: https://www.7-zip.org/
7-Zip source: https://github.com/ip7z/7zip

### License summary

Most of 7-Zip is licensed under the GNU LGPL. `7z.dll` also contains code under
the BSD 2-clause License, the BSD 3-clause License, and an unRAR license
restriction.

The unRAR sources cannot be used to re-create the RAR compression algorithm,
which is proprietary. The code may not be used to develop a RAR (WinRAR)
compatible archiver.

See `resources/7-Zip-License.txt` for the complete license text.

Windows Extra `7za.exe` was not used as the default backend because that reduced
build does not unpack RAR. Clean Extract therefore ships the full 7-Zip console
(`7z.exe` + `7z.dll`) as `7zz.exe`.
