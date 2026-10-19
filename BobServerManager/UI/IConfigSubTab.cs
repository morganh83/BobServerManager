using BobServerManager.Core;

namespace BobServerManager.UI;

/// <summary>Implemented by every sub-tab of the Config Editor so the container can push a freshly
/// loaded Game.ini into each one, and pull their edits back out before saving.</summary>
public interface IConfigSubTab
{
    void RefreshFromDocument(IniDocument doc);
    void ApplyToDocument(IniDocument doc);
}
