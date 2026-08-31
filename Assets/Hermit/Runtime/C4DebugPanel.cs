using UnityEngine;
using UnityEngine.InputSystem;

namespace Hermit.Runtime
{
    /// <summary>
    /// C4 Debug / Temporary — diagnostic-only panel for the Unity ↔ Supabase
    /// spike. NOT real product UI (that is Hermit.UI, built later on top of
    /// uGUI per the Blueprint). Built with IMGUI (OnGUI) on purpose: zero
    /// scene/prefab wiring, trivial to delete wholesale once C4 closes.
    ///
    /// Credentials are typed at runtime only, never serialized to a scene or
    /// asset, and the password field is masked.
    ///
    /// C6 added a collapse toggle (starts collapsed): this panel's fixed
    /// top-left Rect used to sit directly on top of the new game selector's
    /// top-left content. Press F1 to expand it for C4 regression checks —
    /// still Development-only (see HermitRuntimeInstaller), just not visible
    /// by default anymore.
    /// </summary>
    public sealed class C4DebugPanel : MonoBehaviour
    {
        private HermitAppContext _context;
        private string _email = string.Empty;
        private string _password = string.Empty;
        private string _lastResult = string.Empty;
        private string _lastErrorMessage = string.Empty;
        private bool _busy;
        private bool _collapsed = true;

        public void Initialize(HermitAppContext context)
        {
            _context = context;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            {
                _collapsed = !_collapsed;
            }
        }

        private void OnGUI()
        {
            if (_collapsed)
            {
                GUI.Label(new Rect(12, 12, 220, 20), "C4 Debug hidden — press F1");
                return;
            }

            GUILayout.BeginArea(new Rect(12, 12, 440, 660), GUI.skin.box);

            var titleStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 14 };
            GUILayout.Label("C4 Debug / Temporary — not final UI", titleStyle);

            if (GUILayout.Button("Hide (F1)"))
            {
                _collapsed = true;
                GUILayout.EndArea();
                return;
            }

            if (_context == null)
            {
                GUILayout.Label("HermitAppContext not ready (check EnvironmentConfig — see log).");
                GUILayout.EndArea();
                return;
            }

            GUILayout.Space(6);
            GUILayout.Label($"Environment: {_context.Config.Environment}");
            GUILayout.Label($"Connectivity: {(_context.Connectivity.IsConnected ? "reachable" : "unreachable")}");

            var session = _context.Session;
            GUILayout.Label($"Auth state: {(session.HasValue ? "authenticated" : "logged out")}");
            GUILayout.Label($"User id: {(session.HasValue ? session.Value.UserId : "-")}");
            GUILayout.Label($"Display name: {_context.LastDisplayName}");
            GUILayout.Label($"Wallet balance: {_context.LastWalletBalance}");

            GUILayout.Space(10);
            GUILayout.Label("Email");
            _email = GUILayout.TextField(_email);
            GUILayout.Label("Password");
            _password = GUILayout.PasswordField(_password, '*');

            GUI.enabled = !_busy;
            GUILayout.Space(6);

            if (GUILayout.Button("Login"))
            {
                RunAsync(() => _context.LoginAsync(_email, _password));
            }

            if (GUILayout.Button("Refresh"))
            {
                RunAsync(() => _context.RefreshAsync());
            }

            if (GUILayout.Button("Logout"))
            {
                RunAsync(() => _context.LogoutAsync());
            }

            if (GUILayout.Button("Load Profile"))
            {
                RunAsync(() => _context.LoadProfileAsync());
            }

            if (GUILayout.Button("Load Wallet"))
            {
                RunAsync(() => _context.LoadWalletAsync());
            }

            if (GUILayout.Button("Call RPC (get_my_rank)"))
            {
                RunAsync(() => _context.CallSampleRpcAsync());
            }

            GUI.enabled = true;

            GUILayout.Space(10);
            GUILayout.Label("Last result:");
            GUILayout.TextArea(_lastResult, GUILayout.Height(90));

            if (!string.IsNullOrEmpty(_lastErrorMessage))
            {
                GUILayout.Space(6);
                var errorStyle = new GUIStyle(GUI.skin.label) { wordWrap = true };
                errorStyle.normal.textColor = Color.red;
                GUILayout.Label(_lastErrorMessage, errorStyle);
            }

            GUILayout.EndArea();
        }

        private async void RunAsync(System.Func<System.Threading.Tasks.Task<Hermit.Core.HermitResult<string>>> action)
        {
            _busy = true;
            _lastErrorMessage = string.Empty;

            var result = await action();

            if (result.Success)
            {
                _lastResult = result.Value;
            }
            else
            {
                _lastErrorMessage = $"[{result.Error.Kind}] {result.Error.UserMessage}";
            }

            _busy = false;
        }
    }
}
