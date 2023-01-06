# CursorKey
Windowsでmacos/emacs風のカーソルキーを実現するC#スクリプトです。  
次のようなキーバインドを実現します。  
CapsLock＋PNFB ... ↑↓→← (カーソルキー相当)  
CapsLock+A ... Home (テキストエディタなどで行頭に移動 相当)  
CapsLock+E ... End (テキストエディタなどで行末に移動 相当)  
CapsLock+H ... Backspace  
CapsLock+D ... Delete  
また、CapsLockをダブルプレス(?)すると、「CTRLキーをプレス」相当になります。

## 使い方
２つのステップを踏む必要があります…  
+ C#スクリプトをコンパイルしてexeを作成する  
+ CapsLockキーをF13キーに挿げ替える

できたexeファイルを起動します。タスクバーにアイコン(白地に丸)が表示されたら、成功です。

### C#スクリプトをコンパイルしてexeを作成する
コマンドプロンプトでcsc.exeを動かします。  
```
c:¥Windows¥Microsoft.NET¥Framework¥4.0.30319¥csc.exe /optimize+ /platform:anycpu /target:winexe /out:CursorKey.exe CursorKey.cs
```
csc.exeのパスは、ご自身の環境に合わせて変更してください。 　

### CapsLockキーをF13キーに挿げ替える
レジストリを書き換えてCapsLockキーをF13キーに挿げ替えます。F13キーは使えなくなります。  
（CapsLockキーの押下・解除イベント、他のキーと様子が違ってて、うまくハンドルできなかったです。  
　なので、(少なくとも私は)使うことのないF13キーと差し替えて誤魔化すことにしました…）  
レジストリエディタで、"Keyboard Layout"に "Scancode Map"(Binary)を追加し、以下のように編集します。  
00 00 00 00 00 00 00 00  
02 00 00 00 64 00 3A 00  
00 00 00 00

## その他
+ .netの版数  
文字列補間式を使ったので、C#６以降でないとコンパイルできません。  
Windows11は大丈夫みたいですが、Windows10だとプレインストールのdotnetがC#4みたいですのでコンパイル失敗します。  
スクリプトを書き換える、.NETのSDKを入れる、など適宜対処ください。  
+ CursorKeyAleartはメモリリークしてる  
ので、このオブジェクトに関するコードは削除するのが吉です。  

## 書いた人  
2023-01-06 chokuyon (´Д｀)
