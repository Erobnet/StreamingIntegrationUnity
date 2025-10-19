using System;
using GameProject.Inputs;
using Unity.Mathematics;
using UnityEngine.InputSystem;
using System.Runtime.InteropServices;
using UnityEngine;

namespace GameProject
{
    public class StandalonePlayerWindowManagement : MonoBehaviour, IRequireInputAsset
    {
        #region DLLstuff
        const int SWP_HIDEWINDOW = 0x80; //hide window flag.
        const int SWP_SHOWWINDOW = 0x40; //show window flag.
        const int SWP_NOMOVE = 0x0002; //don't move the window flag.
        const int SWP_NOSIZE = 0x0001; //don't resize the window flag.
        const uint WS_SIZEBOX = 0x00040000;
        const int GWL_STYLE = -16;
        const int WS_BORDER = 0x00800000; //window with border
        const int WS_DLGFRAME = 0x00400000; //window with double border but no title
        const int WS_CAPTION = WS_BORDER | WS_DLGFRAME; //window with a title bar
        const int WS_SYSMENU = 0x00080000; //window with no borders etc.
        const int WS_MAXIMIZEBOX = 0x00010000;
        const int WS_MINIMIZEBOX = 0x00020000; //window with minimizebox

        [DllImport("user32.dll")]
        static extern System.IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        static extern int FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(
            System.IntPtr hWnd, // window handle
            System.IntPtr hWndInsertAfter, // placement order of the window
            int X, // x position
            int Y, // y position
            int cx, // width
            int cy, // height
            uint uFlags // window flags.
            );

        [DllImport("user32.dll")]
        static extern System.IntPtr SetWindowLong(
            System.IntPtr hWnd, // window handle
            int nIndex,
            uint dwNewLong
            );

        [DllImport("user32.dll")]
        static extern System.IntPtr GetWindowLong(
            System.IntPtr hWnd,
            int nIndex
            );

        System.IntPtr hWnd;
        System.IntPtr HWND_TOP = new System.IntPtr(0);
        System.IntPtr HWND_TOPMOST = new System.IntPtr(-1);
        System.IntPtr HWND_NOTOPMOST = new System.IntPtr(-2);
        #endregion
        private static readonly WindowVerticalPlacement[] _SupportedWindowY = (WindowVerticalPlacement[])Enum.GetValues(typeof(WindowVerticalPlacement));

        [SerializeField] bool hideWindowBorderAndTitleBar = true;
        [SerializeField] private int[] supportedWindowWidths;
        [SerializeField] private WindowVerticalPlacement _defaultVPlacement;
        [SerializeField] private int _fixedHeight = 270;
        private GameInputsAsset _gameinputAsset;
        private InputAction _changeWindowWidthInputAction;
        private WindowVerticalPlacement _selectedVPlacement;
        private int _currentWindowResolutionIndex = 0;
        private int _windowVerticalPlacementIndex = 0;
        private InputAction _changeWindowYInputAction;
        private Vector2Int _nativeResolution;

        private void Awake()
        {
            hWnd = GetActiveWindow(); //Gets the currently active window handle for use in the user32.dll functions.
            _selectedVPlacement = _defaultVPlacement;
        }

        private void OnChangeVerticalPositionInput(InputAction.CallbackContext context)
        {
            var value = (int)context.ReadValue<float>();
            _windowVerticalPlacementIndex = GetNewIndex(value, _windowVerticalPlacementIndex, _SupportedWindowY.Length);

            var nextRes = supportedWindowWidths[_currentWindowResolutionIndex];
            _selectedVPlacement = _SupportedWindowY[_windowVerticalPlacementIndex];
            ChangeScreenResolution(nextRes, _selectedVPlacement);
        }

        private void OnPerformedChangeWindowsWidthCallback(InputAction.CallbackContext context)
        {
            var value = (int)context.ReadValue<float>();
            _currentWindowResolutionIndex = GetNewIndex(value, _currentWindowResolutionIndex, supportedWindowWidths.Length);

            var nextRes = supportedWindowWidths[_currentWindowResolutionIndex];
            ChangeScreenResolution(nextRes, _selectedVPlacement);
        }

        private static int GetNewIndex(int value, int currentWindowResolutionIndex, int length)
        {
            return math.clamp(currentWindowResolutionIndex + value, 0, length - 1);
        }

        private void ChangeScreenResolution(int nextResWidth, WindowVerticalPlacement selectedVPlacement)
        {
            var _screenPosY = GetScreenPosY(selectedVPlacement);
            ShowWindowBorders(!hideWindowBorderAndTitleBar, 0, _screenPosY, nextResWidth, _fixedHeight, SWP_SHOWWINDOW);
        }

        private int GetScreenPosY(WindowVerticalPlacement selectedVPlacement)
        {
            int _screenPosY;
            switch ( selectedVPlacement )
            {
                case WindowVerticalPlacement.Top:
                    _screenPosY = 0;
                    break;
                default:
                    _screenPosY = _nativeResolution.y - _fixedHeight;
                    break;
            }
            return _screenPosY;
        }

        public void ShowWindowBorders(bool value, int x, int y, int width = 0, int height = 0, uint setWindowPosMask = SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW)
        {
            if ( Application.isEditor ) return; //We don't want to hide the toolbar from our editor!

            int style = GetWindowNativeStyle();
            if ( value )
            {
                SetWindowLong(hWnd, GWL_STYLE, (uint)(style | WS_CAPTION | WS_SIZEBOX)); //Adds caption and the sizebox back.
                SetWindowPos(hWnd, HWND_NOTOPMOST, x, y, width, height, setWindowPosMask); //Make the window normal.
            }
            else
            {
                RemoveTitleBar(style);
                SetWindowPos(x, y, width, height, setWindowPosMask);
            }
        }

        private void SetWindowPos(int x, int y, int width, int height, uint windowFlag)
        {
            SetWindowPos(hWnd, HWND_TOPMOST, x, y, width, height, windowFlag); //Make the window render above toolbar.
        }

        private void SetWindowPosAndSize(int x, int y, int width, int height)
        {
            uint windowFlag = SWP_SHOWWINDOW;
            SetWindowPos(x, y, width, height, windowFlag);
        }

        private int GetWindowNativeStyle()
        {
            return GetWindowLong(hWnd, GWL_STYLE).ToInt32();
        }

        private void RemoveTitleBar(int style)
        {
            SetWindowLong(hWnd, GWL_STYLE, (uint)(style & ~(WS_CAPTION | WS_SIZEBOX))); //removes caption and the sizebox from current style.
        }

        private void OnEnable()
        {
            if ( hideWindowBorderAndTitleBar )
            {
                ShowWindowBorders(false, Screen.mainWindowPosition.x, Screen.mainWindowPosition.y, Screen.width, Screen.height);
            }
        }

        private void Start()
        {
            _nativeResolution = new() {
                x = Display.main.systemWidth,
                y = Display.main.systemHeight
            };

            for ( var index = 0; index < supportedWindowWidths.Length; index++ )
            {
                var windowWidth = supportedWindowWidths[index];
                if ( windowWidth == _nativeResolution.x )
                {
                    _currentWindowResolutionIndex = index;
                    break;
                }
            }
            ChangeScreenResolution(supportedWindowWidths[_currentWindowResolutionIndex], _selectedVPlacement);
        }

        private void OnDestroy()
        {
            _changeWindowWidthInputAction.performed -= OnPerformedChangeWindowsWidthCallback;
            _changeWindowYInputAction.performed -= OnChangeVerticalPositionInput;
        }

        public void SetInputProvider(GameInputsAsset inputAsset)
        {
            _gameinputAsset = inputAsset;
            _changeWindowWidthInputAction = _gameinputAsset.UI.ChangeHorizontalResolution;
            _changeWindowYInputAction = _gameinputAsset.UI.ChangeVerticalPosition;
            _changeWindowYInputAction.performed += OnChangeVerticalPositionInput;
            _changeWindowWidthInputAction.performed += OnPerformedChangeWindowsWidthCallback;
        }
    }

    public enum WindowVerticalPlacement
    {
        Bottom,
        Top,
    }
}