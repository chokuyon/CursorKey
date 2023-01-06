using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Timers;
using System.Windows.Forms;

class CursorKey
{
    static void Main(string[] args)
    {

        // 多重起動のチェック
        bool hasHandle = false;
        System.Threading.Mutex mutex = new System.Threading.Mutex(false, "chokuyon@gmail.com_CursorKey");
        try
        {
            hasHandle = mutex.WaitOne(0, false);
        }
        catch (System.Threading.AbandonedMutexException)
        {
            // 別のアプリケーションがミューテックスを解放しないで終了した、と思われる
            hasHandle = true;
            CursorKeyAleart.WriteLine("別のアプリがミューテックスを解放しないで終了したと思われます。");
        }

        if (!hasHandle)
        {
            MessageBox.Show("多重起動はできません。", "CursorKey");
            return;
        }


        // アプリ起動
        CursorKeyCore cursorKeyCore = new CursorKeyCore();
        cursorKeyCore.Hook();
        Application.Run();


        if (cursorKeyCore != null)
        {
            cursorKeyCore.HookEnd();
            cursorKeyCore.Dispose();
        }

        if (hasHandle)
        {
            mutex.ReleaseMutex();
        }
        mutex.Close();
    }
}

class CursorKeyAleart
{
    public static void WriteLine(string message)
    {
        // Console.WriteLine($"{System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}: {message}");
    }

    public static void StatusViolation(CursorKeyStateMachine.STATUS _status, CursorKeyStateMachine.EVENT _event)
    {
        // CursorKeyAleart.WriteLine($"STATUS VIOLATION STATUS={_status}, EVENT={_event}");
    }
}

class CursorKeyCore
{
    delegate int delegateHookCallback(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    static extern IntPtr SetWindowsHookEx(int idHook, delegateHookCallback lpfn,IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool UnhookWindowsHookEx(IntPtr hHook);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    static extern IntPtr GetModuleHandle(string lpModuleName);

    IntPtr hookPtr = IntPtr.Zero;
    readonly delegateHookCallback hookFunc;

    private CursorKeyStateMachine stateMachine;

    public CursorKeyCore()
    {
        stateMachine = new CursorKeyStateMachine();
        hookFunc = HookCallback;
    }

    public void Dispose()
    {
        stateMachine.Dispose();
    }

    public void Hook()
    {
        using (Process curProcess = Process.GetCurrentProcess())
        using (ProcessModule curModule = curProcess.MainModule)
        {
            hookPtr = SetWindowsHookEx(
                        13,
                        hookFunc,
                        GetModuleHandle(curModule.ModuleName),
                        0
                    );
        }
    }

    int HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        Keys key = (Keys)(short)Marshal.ReadInt32(lParam);

        bool isPress = ((int)wParam == 256) ? true : false;

        /*
         * Shiftキーがプレスされた状態で、カーソル対象キー(PNFB)押されSendInput(カーソルキー)を発行すると、Shiftキーリリースが再帰コールされる。
         * おそらく、SendInputにShiftキープレス修飾をつけないといかんのだと思う。システムがShiftを外しているのだと思われる…
         * たぶん、Windows10ではShiftリリースの再帰コールは無かった、と思うが…。
         * SendInputにShiftキープレス修飾を付ける方法がよくわからないので、対処療法で。
         * SendInputでカーソルキーを送った際に、再帰でShiftキーリリースがきたら無視(破棄)する、再帰動作のフラグを付けて…。
         * デッドロックが怖いけど、ま、とりあえずこれで。(´Д`)
         */
        if (((key == Keys.LShiftKey) || (key == Keys.RShiftKey)) && stateMachine.inProcess)
        {
            //CursorKeyAleart.WriteLine($"[IN] Dispose " + (isPress ? "PRESS   " : "RELEASE ") + key);
            return 1;
        }

        CursorKeyStateMachine.EVENT inputEvent;
        if (key == Keys.F13)
        {
            inputEvent = isPress ? CursorKeyStateMachine.EVENT.F13_PRESS : CursorKeyStateMachine.EVENT.F13_RELEASE;
        }
        else
        {
            inputEvent = isPress ? CursorKeyStateMachine.EVENT.OTHER_KEY_PRESS : CursorKeyStateMachine.EVENT.OTHER_KEY_RELEASE;
        }

        int retCode = stateMachine.ExecuteStateMachineToObtainConvertedKeyCode(inputEvent, key);

        return retCode;
    }

    public void HookEnd()
    {
        UnhookWindowsHookEx(hookPtr);
        hookPtr = IntPtr.Zero;
    }
}

class CursorKeyIcon
{
    NotifyIcon TaskIcon;

    Bitmap IdleImage;
    Bitmap F13Image;
    Bitmap CtrlImage;

    Icon IdleIcon;
    Icon F13Icon;
    Icon CtrlIcon;

    public enum Icons
    {
        Idle,
        F13,
        Ctrl,
        Null,
    }

    public CursorKeyIcon()
    {
        IdleImage = new Bitmap(32, 32);
        F13Image = new Bitmap(32, 32);
        CtrlImage = new Bitmap(32, 32);

        Font workFont = new Font("Meiryo UI", 14);

        Graphics g = Graphics.FromImage(IdleImage);
        g.FillRectangle(Brushes.White, new Rectangle(0, 0, 32, 32));
        g.DrawEllipse(Pens.Black, new Rectangle(2, 2, 32 - 4, 32 - 4));
        g.Dispose();
        IdleIcon = Icon.FromHandle(IdleImage.GetHicon());

        g = Graphics.FromImage(F13Image);
        g.FillRectangle(Brushes.White, new Rectangle(0, 0, 32, 32));
        g.DrawEllipse(Pens.Black, new Rectangle(2, 2, 32 - 4, 32 - 4));
        g.DrawString("M", workFont, Brushes.Black, 4, 4);
        g.Dispose();
        F13Icon = Icon.FromHandle(F13Image.GetHicon());

        g = Graphics.FromImage(CtrlImage);
        g.FillRectangle(Brushes.White, new Rectangle(0, 0, 32, 32));
        g.DrawEllipse(Pens.Red, new Rectangle(2, 2, 32 - 4, 32 - 4));
        g.DrawString("C", workFont, Brushes.Red, 4, 4);
        g.Dispose();
        CtrlIcon = Icon.FromHandle(CtrlImage.GetHicon());

        TaskIcon = new NotifyIcon();
        TaskIcon.Icon = IdleIcon;
        TaskIcon.Visible = true;
        TaskIcon.Text = "CursorKey";

        // コンテキストメニュー
        ContextMenuStrip contextMenuStrip = new ContextMenuStrip();

        ToolStripMenuItem aboutMenuItem = new ToolStripMenuItem();
        aboutMenuItem.Text = "About";
        aboutMenuItem.Image = F13Image;
        aboutMenuItem.Click += (sender, e) =>
        {
            MessageBox.Show(
                "CapsLock + B --> ←" + Environment.NewLine +
                "CapsLock + F --> →" + Environment.NewLine +
                "CapsLock + P --> ↑" + Environment.NewLine +
                "CapsLock + N --> ↓" + Environment.NewLine +
                "CapsLock + A --> Home" + Environment.NewLine +
                "CapsLock + E --> End" + Environment.NewLine +
                "CapsLock + H --> BS" + Environment.NewLine +
                "CapsLock + D --> Del" + Environment.NewLine,
                "About CursorKey"
                );
        };
        contextMenuStrip.Items.Add(aboutMenuItem);

        ToolStripMenuItem quitMenuItem = new ToolStripMenuItem();
        quitMenuItem.Text = "Quit";
        // quitMenuItem.Image = SystemIcons.Exclamation.ToBitmap();
        quitMenuItem.Click += ToolStripMenuItem_Click_Quit;
        contextMenuStrip.Items.Add(quitMenuItem);

        TaskIcon.ContextMenuStrip = contextMenuStrip;
    }

    private void ToolStripMenuItem_Click_Quit(object sender, EventArgs e)
    {
        this.Dispose();
        Application.Exit();
    }

    public void Dispose()
    {
        TaskIcon.Visible = false;
        TaskIcon.Icon = null;
    }

    public void ChangeState(Icons icon)
    {
        switch(icon)
        {
            case Icons.Idle:
                TaskIcon.Icon = IdleIcon;
                break;
            case Icons.F13:
                TaskIcon.Icon = F13Icon;
                break;
            case Icons.Ctrl:
                TaskIcon.Icon = CtrlIcon;
                break;
            case Icons.Null:
                TaskIcon.Icon = null;
                break;
        }
    }
}

class CursorKeyStateMachine
{
    [DllImport("user32.dll")]
    static extern void SendInput(int nInputs, ref INPUT pInputs, int cbsize);

    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public int mouseData;
        public int dwFlags;
        public int time;
        public IntPtr dwExtraInfo;
    };

    // Virtual key code
    // https://learn.microsoft.com/ja-jp/windows/win32/inputdev/virtual-key-codes
    public const short VIRTUAL_KEY_CODE_RIGHT = 0x27;   // The RIGHT ARROW key.
    public const short VIRTUAL_KEY_CODE_LEFT = 0x25;    // The LEFT ARROW key.
    public const short VIRTUAL_KEY_CODE_UP = 0x26;      // The UP ARROW key.
    public const short VIRTUAL_KEY_CODE_DOWN = 0x28;    // The DOWN ARROW key.
    public const short VIRTUAL_KEY_CODE_HOME = 0x24;    // The HOME key.
    public const short VIRTUAL_KEY_CODE_END = 0x23;     // The END key.
    public const short VIRTUAL_KEY_CODE_BACK = 0x08;    // The BACKSPACE key.
    public const short VIRTUAL_KEY_CODE_DELETE = 0x2E;  // The DELETE key.
    public const short VIRTUAL_KEY_CODE_CTRL = 0x11;    // The CTRL modifier key.

    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT
    {
        public short wVk;
        public short wScan;
        public int dwFlags;
        public int time;
        public IntPtr dwExtraInfo;
    };

    [StructLayout(LayoutKind.Sequential)]
    struct HARDWAREINPUT
    {
        public int uMsg;
        public short wParamL;
        public short wParamH;
    };

    const int INPUT_TYPE_MOUSE = 0;
    const int INPUT_TYPE_KEYBOARD = 1;
    const int INPUT_TYPE_HARDWARE = 2;

    [StructLayout(LayoutKind.Sequential)]
    struct INPUT
    {
        public int type;
        public INPUT_UNION uni;     // ここのアライメント、ビルドターゲットが32bitなら4byte、64bitなら8byte。Explicitで直接指定しないでコンパイラに解決させる
    };

    [StructLayout(LayoutKind.Explicit)]
    struct INPUT_UNION
    {
        [FieldOffset(0)]
        public MOUSEINPUT mi;
        [FieldOffset(0)]
        public KEYBDINPUT ki;
        [FieldOffset(0)]
        public HARDWAREINPUT hi;
    };

    // F13キー管理用ステートマシンのステータス(状態)
    public enum STATUS
    {
        IDLE = 0,                       // 0: 初期状態
        PRE_F13_PRESS,                  // 1: F13キープレスモード(カーソルキーモード)へ移行する前状態・F13キーをプレスしている状態
        PRE_F13_RELEASE,                // 2: F13キープレスモード(カーソルキーモード)へ移行する前状態・F13キーをリリースしている状態
        PRE_CTRL_PRESS,                 // 3: CTRLキープレスモード(CTRLキープレスモード)へ移行する前状態・F13キーをプレスしている状態
        F13_PRESS,                      // 4: F13キープレスモード
        CTRL_PRESS,                     // 5: CTRLキープレスモード
    }
    private STATUS status;

    // F13キー管理用ステートマシンに入力するイベント
    public enum EVENT
    {
        F13_PRESS = 0,                  // F13キープレス
        F13_RELEASE,                    // F13キーリリース
        OTHER_KEY_PRESS,                // F13キー以外をプレス
        OTHER_KEY_RELEASE,              // F13キー以外をリリース
        TIMER_UP,                       // F13キーダブルプレスにおける 初回プレスから初回リリース、初回リリースから2回目プレス の間隔を監視するタイマーが満了した
        UNKOWN,                         // 未定義
    }

    private CursorKeyIcon taskTrayIcon;

    public bool inProcess = false;

    public CursorKeyStateMachine()
    {
        status = STATUS.IDLE;
        doublePressIntervalTimer = CreateDoublePressIntervalTimer(150);         // 150[msec]

        taskTrayIcon = new CursorKeyIcon();
    }

    public void Dispose()
    {
        doublePressIntervalTimer.Dispose();
        taskTrayIcon.Dispose();
    }


    /*
        ____________  STATUS|                        |                        |                        |                        |                        |                        |
                    |       | 0:IDLE                 | 1:PRE_F13_PRESS        | 2:PRE_F13_RELEASE      | 3:PRE_CTRL_PRESS       | 4:F13_PRESS            | 5:CTRL_PRESS           |
        EVENT        |      |                        |                        |                        |                        |                        |                        |
        --------------------+------------------------+------------------------+------------------------+------------------------+------------------------+------------------------+
        0:F13_PRESS         | Timer_start            | Dispose_key_press      | Timer_stop             | Dispose_key_press      | Dispose_key_press      | Dispose_key_press      |
                            | Dispose_key            |                        | Timer_start            |                        |                        |                        |
                            | Change_icon_state(F13) |                        | Send_ctrl_key_press    |                        |                        |                        |
                            |                        |                        | Dispose_key_press      |                        |                        |                        |
                            |                        |                        | Change_icon_state(CTRL)|                        |                        |                        |
                            | => 1:PRE_F13_PRESS     | => *                   | => 3:PRE_CTRL_PRESS    | => *                   | => *                   | => *                   |
        --------------------+------------------------+------------------------+------------------------+------------------------+------------------------+------------------------+
        1:F13_RELEASE       | Dispose_key            | Timer_stop             | -                      | Timer_stop             | Change_icon_state(IDLE)| Send_ctrl_key_release  |
                            |                        | Timer_start            | (status violation...)  | Timer_start            | Dispose_key_release    | Dispose_key_release    |
                            |                        | Dispose_key_release    |                        | Send_ctrl_key_release  |                        | Change_icon_state(IDEL)|
                            |                        | Change_icon_state(IDLE)|                        | Dispose_key_release    |                        |                        |
                            |                        |                        |                        | Change_icon_state(IDLE)|                        |                        |
                            | => *                   | => 2:PRE_F13_RELEASE   | => *                   | => 2:PRE_F13_RELEASE   | => 0:IDLE              | => 0:IDLE              |
        --------------------+------------------------+------------------------+------------------------+------------------------+------------------------+------------------------+
        2:OTHER_KEY_PRESS   | Throw_key_press        | ? isCursorKey == True  | Timer_stop             | Throw_key_press        | ? isCursorKey == True  | Throw_key_press        |
                            |                        |   Send_cursor_press    | Throw_key_press        |                        |   Send_cursor_press    |                        |
                            |                        |   Dispose_key_press    |                        |                        |   Dispose_key_press    |                        |
                            |                        | ---------------------- |                        |                        | ---------------------- |                        |
                            |                        |   Throw_key_press      |                        |                        |   Throw_key_press      |                        |
                            | => *                   | => *                   | => 0:IDLE        *(3,2)| => *                   | => *                   | => *                   |
        --------------------+------------------------+------------------------+------------------------+------------------------+------------------------+------------------------+
        3:OTHER_KEY_RELEASE | Throw_key_release      | ? isCursorKey == True  | Throw_key_relase       | Throw_key_release      | ? isCursorKey == True  | Throw_key_release      |
                            |                        |   Send_cursor_release  |                        |                        |   Send_cursor_release  |                        |
                            |                        |   Dispose_key_release  |                        |                        |   Dispose_key_release  |                        |
                            |                        | ---------------------- |                        |                        | ---------------------- |                        |
                            |                        |   Throw_key_release    |                        |                        |   Throw_key_release    |                        |
                            | => *                   | => *                   | => *             *(3,3)| => *                   | => *                   | => *                   |
        --------------------+------------------------+------------------------+------------------------+------------------------+------------------------+------------------------+
        4:TIMER_UP          | -                      | -                      | -                      | -                      | -                      | -                      |
                            | (status violation)     |                        |                        |                        | (status violation)     | (status violation)     |
                            |                        |                        |                        |                        |                        |                        |
                            |                        |                        |                        |                        |                        |                        |
                            | => *                   | => 4:F13_PRESS         | => 0:IDLE              | => 5:CTRL_PRESS        | => *                   | => *                   |
        --------------------+------------------------+------------------------+------------------------+------------------------+------------------------+------------------------+
    【マトリクスの注釈】
     *(3,2) F13キー2回目プレス待ち中にF13以外のキーがプレスされたら、F13二重プレスの動作をキャンセルする
     *(3,3) F13キー2回目プレス待ち中にF13以外のキーがリリースされても、F13二重プレスの動作をキャンセルしない(F13二重プレスは他キーリリースでキャンセルしない)
    */
    public int ExecuteStateMachineToObtainConvertedKeyCode(CursorKeyStateMachine.EVENT inputEvent, Keys key)
    {
        int retCode = 0;    // 0:フックしたキー入力をスルーする(Throw_xx_xx)、1:フックしたキー入力を破棄する(Dispose_key)
        CursorKeyAleart.WriteLine($"[IN] EVENT: {inputEvent} ({key})  STATUS: {this.status}");

        switch(this.status)
        {
            case STATUS.IDLE:
                retCode = DoStatus_IDLE(inputEvent);
                break;

            case STATUS.PRE_F13_PRESS:
                retCode = DoStatus_PRE_F13_PRESS(inputEvent, key);
                break;

            case STATUS.PRE_F13_RELEASE:
                retCode = DoStatus_PRE_F13_RELEASE(inputEvent);
                break;

            case STATUS.PRE_CTRL_PRESS:
                retCode = DoState_PRE_CTRL_PRESS(inputEvent);
                break;

            case STATUS.F13_PRESS:
                retCode = DoState_F13_PRESS(inputEvent, key);
                break;

            case STATUS.CTRL_PRESS:
                retCode = DoStatus_CTRL_PRESS(inputEvent);
                break;
        }

        CursorKeyAleart.WriteLine($"  -> [OUT] EVENT: {inputEvent} ({key})  STATUS: {this.status}　retCode={retCode}");

        return (retCode);
    }

    private int DoStatus_IDLE(EVENT inputEvent)
    {
        CursorKeyAleart.WriteLine($" [DoStatus_IDLE] event={inputEvent}");

        int retCode = 0;    // 0:フックしたキー入力をスルーする(Throw_xx_xx)、1:フックしたキー入力を破棄する(Dispose_key)

        switch(inputEvent)
        {
            case EVENT.F13_PRESS:
                doublePressIntervalTimer.Start();
                retCode = 1;    // Dispose_key
                taskTrayIcon.ChangeState(CursorKeyIcon.Icons.F13);

                status = STATUS.PRE_F13_PRESS;
                break;

            case EVENT.F13_RELEASE:
                retCode = 1;    // Dispose_key
                break;

            case EVENT.OTHER_KEY_PRESS:
                // retCode = 0; // Throw_key_press
            case EVENT.OTHER_KEY_RELEASE:
                // retCode = 0; // Throw_key_release
                break;

            case EVENT.TIMER_UP:
                CursorKeyAleart.StatusViolation(status, inputEvent);
                break;
        }

        return(retCode);
    }

    private int DoStatus_PRE_F13_PRESS(EVENT inputEvent, Keys key)
    {
        CursorKeyAleart.WriteLine($" [DoState_PRE_F13_PRESS] event={inputEvent}, key={key}");

        int retCode = 0;    // 0:フックしたキー入力をスルーする(Throw_xx_xx)、1:フックしたキー入力を破棄する(Dispose_key)

        switch(inputEvent)
        {
            case EVENT.F13_PRESS:
                retCode = 1;    // Dispose_key_press
                break;

            case EVENT.F13_RELEASE:
                doublePressIntervalTimer.Stop();
                doublePressIntervalTimer.Start();
                retCode = 1;    // Dispose_key_release
                taskTrayIcon.ChangeState(CursorKeyIcon.Icons.Idle);

                status = STATUS.PRE_F13_RELEASE;
                break;

            case EVENT.OTHER_KEY_PRESS:
                if (IsCursorKey(key))
                {
                    ExchangeKeyAndSendInput(key, true);         // send_cursor_press
                    retCode = 1;                                // Dispose_key_press
                }
                //else
                //{
                //    retCode = 0; // Throw_key_press
                //}
                break;

            case EVENT.OTHER_KEY_RELEASE:
                if (IsCursorKey(key))
                {
                    ExchangeKeyAndSendInput(key, false);        // send_cursor_release
                    retCode = 1;                                // Dispose_key_release
                }
                //else
                //{
                //     retCode = 0; // Throw_key_release
                //}
                break;

            case EVENT.TIMER_UP:
                status = STATUS.F13_PRESS;
                break;
        }

        return(retCode);
    }

    private int DoStatus_PRE_F13_RELEASE(EVENT inputEvent)
    {
        CursorKeyAleart.WriteLine($" [DoState_PRE_F13_RELEASE] event={inputEvent}");

        int retCode = 0;    // 0:フックしたキー入力をスルーする(Throw_xx_xx)、1:フックしたキー入力を破棄する(Dispose_key)

        switch(inputEvent)
        {
            case EVENT.F13_PRESS:
                doublePressIntervalTimer.Stop();
                doublePressIntervalTimer.Start();
                SendKey(VIRTUAL_KEY_CODE_CTRL, true);       // Send_ctrl_key_press
                retCode = 1;                                // Dispose_key_press
                taskTrayIcon.ChangeState(CursorKeyIcon.Icons.Ctrl);

                status = STATUS.PRE_CTRL_PRESS;
                break;

            case EVENT.F13_RELEASE:
                // status violation...
                CursorKeyAleart.WriteLine($"STATUS VIOLATION STATUS={status}, EVENT={inputEvent}");
                break;

            case EVENT.OTHER_KEY_PRESS:
                doublePressIntervalTimer.Stop();
                // retCode = 0; // Throw_key_press

                status = STATUS.IDLE;
                break;

            case EVENT.OTHER_KEY_RELEASE:
                // retCode = 0; // Throw_key_release;
                break;

            case EVENT.TIMER_UP:
                status = STATUS.IDLE;
                break;
        }

        return(retCode);
    }

    private int DoState_PRE_CTRL_PRESS(EVENT inputEvent)
    {
        CursorKeyAleart.WriteLine($" [DoState_PRE_CTRL_PRESS] event={inputEvent}");

        int retCode = 0;    // 0:フックしたキー入力をスルーする(Throw_xx_xx)、1:フックしたキー入力を破棄する(Dispose_key)

        switch (inputEvent)
        {
            case EVENT.F13_PRESS:
                retCode = 1;    // Dispose_key_press
                break;

            case EVENT.F13_RELEASE:
                doublePressIntervalTimer.Stop();
                doublePressIntervalTimer.Start();
                SendKey(VIRTUAL_KEY_CODE_CTRL, false);  // Send_ctrl_key_release
                retCode = 1;                            // Dispose_key_release
                taskTrayIcon.ChangeState(CursorKeyIcon.Icons.Idle);

                status = STATUS.PRE_F13_RELEASE;
                break;

            case EVENT.OTHER_KEY_PRESS:
                // retCode = 0;     // Throw_key_press
                // break;

            case EVENT.OTHER_KEY_RELEASE:
                // retCode = 0;     // Throw_key_press
                break;

            case EVENT.TIMER_UP:
                status = STATUS.CTRL_PRESS;
                break;
        }

        return (retCode);
    }

    private int DoState_F13_PRESS(EVENT inputEvent, Keys key)
    {
        CursorKeyAleart.WriteLine($" [DoState_F13_PRESS] event={inputEvent}");

        int retCode = 0;    // 0:フックしたキー入力をスルーする(Throw_xx_xx)、1:フックしたキー入力を破棄する(Dispose_key)

        switch(inputEvent)
        {
            case EVENT.F13_PRESS:
                retCode = 1;            // Dispose_key_press
                break;

            case EVENT.F13_RELEASE:
                taskTrayIcon.ChangeState(CursorKeyIcon.Icons.Idle);
                retCode = 1;            // Dispose_key_release

                status = STATUS.IDLE;
                break;

            case EVENT.OTHER_KEY_PRESS:
                if (IsCursorKey(key))
                {
                    
                    // Throw_cursor_press
                    ExchangeKeyAndSendInput(key, true);         // Send_cursor_press
                    retCode = 1;                                // Dispose_key_press
                }
                //else
                //{
                //    retCode = 0;  // Throw_key_press
                //}
                break;

            case EVENT.OTHER_KEY_RELEASE:
                if (IsCursorKey(key))
                {
                    // Throw_cursor_release
                    ExchangeKeyAndSendInput(key, false);        // Send_cursor_release
                    retCode = 1;                                // Dispose_key_press
                }
                //else
                //{
                //    retCode = 0; // Throw_key_release
                //}
                break;
                
            case EVENT.TIMER_UP:
                CursorKeyAleart.StatusViolation(status, inputEvent);
                break;
        }

        return(retCode);
    }

    private int DoStatus_CTRL_PRESS(EVENT inputEvent)
    {
        CursorKeyAleart.WriteLine($" [DoState_CTRL_PRESS] event={inputEvent}");

        int retCode = 0;    // 0:フックしたキー入力をスルーする(Throw_xx_xx)、1:フックしたキー入力を破棄する(Dispose_key)

        switch(inputEvent)
        {
            case EVENT.F13_PRESS:
                retCode = 1;    // Dispose_key_press
                break;

            case EVENT.F13_RELEASE:
                SendKey(VIRTUAL_KEY_CODE_CTRL, false);          // Send_ctrl_key_release
                retCode = 1;                                    // Dispose_key_release
                taskTrayIcon.ChangeState(CursorKeyIcon.Icons.Idle);

                status = STATUS.IDLE;
                break;

            case EVENT.OTHER_KEY_PRESS:
                // retCode = 0; // Throw_key_press
                // break;

            case EVENT.OTHER_KEY_RELEASE:
                // retCode = 0; // Throw_key_release
                break;

            case EVENT.TIMER_UP:
                CursorKeyAleart.StatusViolation(status, inputEvent);
                break;
        }

        return(retCode);
    }



    private static bool IsCursorKey(Keys key)
    {
        switch(key)
        {
            case Keys.F:        // Right arrow
            case Keys.B:        // Left arrow
            case Keys.P:        // Up arrow
            case Keys.N:        // Down arrow
            case Keys.A:        // Home
            case Keys.E:        // End
            case Keys.H:        // Backspace
            case Keys.D:        // Delete
                return true;
            default:
                return false;
        }
    }

    // fake エラー発生は例外にしようよ…
    private void SendKey(short key, bool isKeyPress)
    {
        INPUT input;
        if (isKeyPress)
        {
            input = new INPUT
            {
                type = INPUT_TYPE_KEYBOARD,
                uni = new INPUT_UNION
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = key,
                        wScan = 0,
                        dwFlags = 0,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            };
        }
        else
        {
            input = new INPUT
            {
                type = INPUT_TYPE_KEYBOARD,
                uni = new INPUT_UNION
                {
                    ki = new KEYBDINPUT
                    {
                        wVk = key,
                        wScan = 0,
                        dwFlags = 2,
                        time = 0,
                        dwExtraInfo = IntPtr.Zero,
                    },
                },
            };
        }

        this.inProcess= true;
        SendInput(1, ref input, Marshal.SizeOf(input));
        this.inProcess= false;
    }

    // fake エラー発生は例外にしようよ…
    private void ExchangeKeyAndSendInput(Keys key, bool isKeyPress)
    {
        short convertedKeyCode;

        switch (key)
        {
            case Keys.F:    // Right arrow
                convertedKeyCode = VIRTUAL_KEY_CODE_RIGHT;
                break;
            case Keys.B:    // Left arrow
                convertedKeyCode = VIRTUAL_KEY_CODE_LEFT;
                break;
            case Keys.P:    // Up arrow
                convertedKeyCode = VIRTUAL_KEY_CODE_UP;
                break;
            case Keys.N:    // Down arrow
                convertedKeyCode = VIRTUAL_KEY_CODE_DOWN;
                break;
            case Keys.A:    // Home
                convertedKeyCode = VIRTUAL_KEY_CODE_HOME;
                break;
            case Keys.E:    // End
                convertedKeyCode = VIRTUAL_KEY_CODE_END;
                break;
            case Keys.H:    // Backspace
                convertedKeyCode = VIRTUAL_KEY_CODE_BACK;
                break;
            case Keys.D:    // Delete
                convertedKeyCode = VIRTUAL_KEY_CODE_DELETE;
                break;
            default:
                CursorKeyAleart.WriteLine($"Key: {key} はカーソルキーではない。");
                return;
        }

        SendKey(convertedKeyCode, isKeyPress);
    }

    // ダブルプレスにおける 初回プレスー初回リリース、初回リリースー２回目プレスの間隔をチェックするタイマー
    private System.Timers.Timer doublePressIntervalTimer;

    private System.Timers.Timer CreateDoublePressIntervalTimer(int interval)
    {
        System.Timers.Timer createdTimer = new System.Timers.Timer()
        {
            Interval = interval,
            AutoReset = false,
            Enabled = false,
        };
        createdTimer.Elapsed += eventDoublePressIntervalTimer;

        return createdTimer;
    }

    private void eventDoublePressIntervalTimer(Object source, ElapsedEventArgs e)
    {
        ExecuteStateMachineToObtainConvertedKeyCode(EVENT.TIMER_UP, Keys.None);
    }
}
