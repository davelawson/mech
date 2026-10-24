using Godot;

namespace Mech;

public partial class CommandApp : Control
{
    public OfficeScreen Office { get; private set; } = null!;
    public MapScreen StarMap { get; private set; } = null!;
    private ColorRect _header = null!;
    private Label _date = null!;

    public override void _Ready()
    {
        Theme = Ui.CreateTheme();
        GetWindow().MinSize = new Vector2I(900, 500);
        Office = GetNode<OfficeScreen>("Office");
        StarMap = GetNode<MapScreen>("StarMap");
        _header = new ColorRect { Color = Ui.Panel, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_header);
        var brand = Ui.Label(_header, "◇  M E C H    |    MERCENARY COMMAND", 16, Ui.Gold);
        Ui.Place(brand, 36, 0, 480, 72);
        _date = Ui.Label(_header, "●   01 JAN 3025", 13, Ui.Gold);
        _date.HorizontalAlignment = HorizontalAlignment.Right;
        Office.MapRequested += ShowMap;
        StarMap.BackRequested += ShowOffice;
        Resized += Layout;
        Layout();
        ShowOffice();
    }

    private void Layout()
    {
        if (_header is null) return;
        Ui.Place(_header, 0, 0, Size.X, 72);
        Ui.Place(_date, Size.X - 210, 0, 174, 72);
        Ui.Place(Office, 0, 72, Size.X, Size.Y - 72);
        Ui.Place(StarMap, 0, 72, Size.X, Size.Y - 72);
    }

    public void ShowMap()
    {
        Office.Hide();
        StarMap.Show();
        StarMap.Viewport.CallDeferred(Control.MethodName.GrabFocus);
        DisplayServer.WindowSetTitle("Mech · Inner Sphere");
    }

    public void ShowOffice()
    {
        StarMap.Viewport.EndDrag();
        StarMap.Hide();
        Office.Show();
        Office.Hotspot.CallDeferred(Control.MethodName.GrabFocus);
        DisplayServer.WindowSetTitle("Mech · Command");
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (StarMap.Visible && @event is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            ShowOffice();
            GetViewport().SetInputAsHandled();
        }
    }
}
