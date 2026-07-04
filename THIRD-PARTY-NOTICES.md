# Third-Party Notices

This repository contains source code adapted from third-party projects. The original
licenses are reproduced below.

## System.Reactive (dotnet/reactive)

The disposable types under `src/Copse/Disposables/` (`Disposable`, `AnonymousDisposable`,
`RefCountDisposable`, `CompositeDisposable`, `ICancelable`) are lifted from
[System.Reactive](https://github.com/dotnet/reactive) (namespace
`System.Reactive.Disposables`) at commit
[`94b5d5ab912789f5abe9a72138a25bbd716fe59c`](https://github.com/dotnet/reactive/tree/94b5d5ab912789f5abe9a72138a25bbd716fe59c),
with syntax adapted for this repository's language level and Rx resource strings inlined.
Semantics are unchanged. Each lifted file carries an attribution header.

```
MIT License

Copyright (c) .NET Foundation and Contributors
All Rights Reserved

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```
