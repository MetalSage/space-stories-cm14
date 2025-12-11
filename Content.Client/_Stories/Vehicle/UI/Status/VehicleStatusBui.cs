using Content.Client._Stories.UserInterface.Control;
using Content.Shared._Stories.Attachables;
using Content.Shared._Stories.Vehicle;
using Content.Shared.Damage;
using Content.Shared.Explosion.Components;
using Content.Shared.FixedPoint;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._Stories.Vehicle.UI.Status;

[UsedImplicitly]
public sealed class VehicleStatusBui : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private VehicleStatusWindow? _window;
    private VehicleStatusUIState? _lastState;

    private bool _resistancesExpanded = false;
    private bool _passengersExpanded = false;

    public VehicleStatusBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<VehicleStatusWindow>();

        _window.ResistancesToggle.OnPressed += _ => ToggleResistances();
        _window.PassengersToggle.OnPressed += _ => TogglePassengers();

        UpdateToggleButtonTexts();
        Refresh();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is VehicleStatusUIState vs)
        {
            _lastState = vs;
            Refresh();
        }
    }

    private void ToggleResistances()
    {
        if (_window == null)
            return;

        _resistancesExpanded = !_resistancesExpanded;
        _window.ResistancesContainer.Visible = _resistancesExpanded;
        UpdateToggleButtonTexts();
    }

    private void TogglePassengers()
    {
        if (_window == null)
            return;

        _passengersExpanded = !_passengersExpanded;
        _window.PassengersContentContainer.Visible = _passengersExpanded;
        UpdateToggleButtonTexts();
    }

    private void UpdateToggleButtonTexts()
    {
        if (_window == null)
            return;

        _window.ResistancesToggle.Text = Loc.GetString("st-ui-vehicle-armor-resistances", ("unfolded", _resistancesExpanded));
        _window.PassengersToggle.Text = Loc.GetString("st-ui-vehicle-passengers", ("unfolded", _passengersExpanded));
    }

    public void Refresh()
    {
        if (_window is not { IsOpen: true })
            return;

        if (!EntMan.TryGetComponent(Owner, out VehicleComponent? vehicle))
            return;

        UpdateIntegrity(vehicle);
        UpdateDoorLock();
        UpdateResistances(vehicle);
        UpdatePassengers(vehicle);
        UpdateHardpoints();
    }

    private void UpdateIntegrity(VehicleComponent vehicle)
    {
        if (_window == null) return;

        float integrity = 0f;
        if (EntMan.TryGetComponent<DamageableComponent>(Owner, out var damageable))
        {
            var currentHealth = FixedPoint2.Max(vehicle.MaxHealth - damageable.TotalDamage, 0);
            integrity = vehicle.MaxHealth > 0 ? (float)(currentHealth / vehicle.MaxHealth) * 100f : 0f;
        }

        if (Math.Abs(_window.IntegrityProgressBar.Value - integrity) > 0.01f)
            _window.IntegrityProgressBar.Value = integrity;

        var labelText = integrity <= 0 || vehicle.Destroyed
            ? Loc.GetString("st-ui-vehicle-hull-destroyed")
            : Loc.GetString("st-ui-vehicle-hull-integrity", ("integrity", integrity.ToString("F0")));

        if (_window.IntegrityProgressBar.Label.Text != labelText)
            _window.IntegrityProgressBar.Label.Text = labelText;

        _window.IntegrityProgressBar.ForegroundStyleBoxOverride = GetIntegrityStyleBox(integrity, vehicle.Destroyed);
    }

    private StyleBoxFlat GetIntegrityStyleBox(float integrity, bool destroyed)
    {
        if (destroyed || integrity <= 0)
        {
            return new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#7A1A1A"),
                BorderColor = Color.FromHex("#D32F2F"),
                BorderThickness = new Thickness(1)
            };
        }

        if (integrity >= 70)
            return new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#2E7D32"),
                BorderColor = Color.FromHex("#4CAF50"),
                BorderThickness = new Thickness(1)
            };
        else if (integrity >= 40)
            return new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#EF6C00"),
                BorderColor = Color.FromHex("#FF9800"),
                BorderThickness = new Thickness(1)
            };
        else if (integrity >= 20)
            return new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#C62828"),
                BorderColor = Color.FromHex("#F44336"),
                BorderThickness = new Thickness(1)
            };
        else
            return new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#7A1A1A"),
                BorderColor = Color.FromHex("#D32F2F"),
                BorderThickness = new Thickness(1)
            };
    }

    private void UpdateDoorLock()
    {
        if (_window == null)
            return;

        var locked = _lastState?.DoorState ?? false;

        _window.DoorLockLabel.Text = Loc.GetString("st-ui-vehicle-door-state", ("locked", locked));

        if (locked)
        {
            _window.DoorLockPanel.PanelOverride = new StyleBoxFlat
            {
                BorderColor = Color.FromHex("#D32F2F"),
                BorderThickness = new Thickness(2),
                BackgroundColor = Color.FromHex("#2A1A1A")
            };
            _window.DoorLockLabel.FontColorOverride = Color.FromHex("#F44336");
        }
        else
        {
            _window.DoorLockPanel.PanelOverride = new StyleBoxFlat
            {
                BorderColor = Color.FromHex("#4CAF50"),
                BorderThickness = new Thickness(2),
                BackgroundColor = Color.FromHex("#1A2A1A")
            };
            _window.DoorLockLabel.FontColorOverride = Color.FromHex("#4CAF50");
        }
    }

    private void UpdateResistances(VehicleComponent vehicle)
    {
        if (_window == null)
            return;

        _window.ResistancesContainer.RemoveAllChildren();

        if (!EntMan.TryGetComponent<DamageableComponent>(Owner, out var damageable))
            return;

        foreach (var (damageType, value) in vehicle.DamageMults)
        {
            var resistance = (1f - value) * 100f;
            var resistanceColor = GetResistanceColor(resistance);

            var resistanceRow = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                HorizontalExpand = true,
                Margin = new Thickness(0, 3)
            };

            var typeLabel = new Label
            {
                Text = Loc.GetString("st-ui-vehicle-resistance-entry", ("type", damageType)),
                HorizontalExpand = true,
                FontColorOverride = Color.FromHex("#E0E0E0")
            };

            var percentLabel = new Label
            {
                Text = $"{resistance:+#;-#;0}%",
                FontColorOverride = resistanceColor
            };

            resistanceRow.AddChild(typeLabel);
            resistanceRow.AddChild(percentLabel);

            _window.ResistancesContainer.AddChild(resistanceRow);
        }

        if (EntMan.TryGetComponent<ExplosionResistanceComponent>(Owner, out var explResistance))
        {
            var separator = new PanelContainer
            {
                MinHeight = 1,
                Margin = new Thickness(0, 6),
                PanelOverride = new StyleBoxFlat
                {
                    BackgroundColor = Color.FromHex("#3A3A3A")
                }
            };
            _window.ResistancesContainer.AddChild(separator);

            var overall = (1f - explResistance.DamageCoefficient) * 100f;
            var overallColor = GetResistanceColor(overall);

            var overallRow = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                HorizontalExpand = true,
                Margin = new Thickness(0, 3)
            };

            var overallLabel = new Label
            {
                Text = Loc.GetString("st-ui-vehicle-resistance-entry", ("type", "Expl")),
                HorizontalExpand = true,
                FontColorOverride = Color.FromHex("#E0E0E0")
            };

            var overallPercent = new Label
            {
                Text = $"{overall:+#;-#;0}%",
                FontColorOverride = overallColor
            };

            overallRow.AddChild(overallLabel);
            overallRow.AddChild(overallPercent);
            _window.ResistancesContainer.AddChild(overallRow);

            foreach (var (explType, coeff) in explResistance.Modifiers)
            {
                var typeResist = (1f - coeff) * 100f;
                var typeColor = GetResistanceColor(typeResist);

                var modRow = new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    HorizontalExpand = true,
                    Margin = new Thickness(10, 2, 0, 2)
                };

                var modLabel = new Label
                {
                    Text = Loc.GetString("st-ui-vehicle-resistance-entry", ("type", "Expl")),
                    HorizontalExpand = true,
                    FontColorOverride = Color.FromHex("#B0B0B0")
                };

                var modPercent = new Label
                {
                    Text = $"{typeResist:+#;-#;0}%",
                    FontColorOverride = typeColor
                };

                modRow.AddChild(modLabel);
                modRow.AddChild(modPercent);
                _window.ResistancesContainer.AddChild(modRow);
            }
        }
    }

    private Color GetResistanceColor(float resistance)
    {
        return resistance switch
        {
            > 50 => Color.FromHex("#4CAF50"),
            > 20 => Color.FromHex("#FFA500"),
            > 0 => Color.FromHex("#FF5722"),
            _ => Color.FromHex("#F44336")
        };
    }

    private void UpdatePassengers(VehicleComponent vehicle)
    {
        if (_window == null)
            return;

        _window.PassengerCategoriesContainer.RemoveAllChildren();

        if (vehicle.PassengerSlots.Max > 0)
            AddPassengerCategory("st-ui-vehicle-passengers-category", vehicle.PassengerSlots, "#4CAF50");

        if (vehicle.RevivableDeadSlots.Max > 0)
            AddPassengerCategory("st-ui-vehicle-dead-category", vehicle.RevivableDeadSlots, "#FF9800");

        foreach (var roleGroup in vehicle.RoleReservedSlots)
        {
            var slotRow = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                HorizontalExpand = true,
                Margin = new Thickness(0, 3)
            };

            var nameLabel = new Label
            {
                Text = Loc.GetString("st-ui-vehicle-role-reserved-slot", ("name", roleGroup.CategoryName)),
                HorizontalExpand = true,
                FontColorOverride = Color.FromHex("#E0E0E0")
            };

            var countLabel = new Label
            {
                Text = $"{roleGroup.Total.Current}/{roleGroup.Total.Max}",
                FontColorOverride = GetSlotColor(roleGroup.Total.Current, roleGroup.Total.Max)
            };

            slotRow.AddChild(nameLabel);
            slotRow.AddChild(countLabel);
            _window.PassengerCategoriesContainer.AddChild(slotRow);
        }
    }

    private void AddPassengerCategory(string locKey, SlotCount slots, string colorHex)
    {
        if (_window == null)
            return;

        var slotRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            Margin = new Thickness(0, 3)
        };

        var nameLabel = new Label
        {
            Text = Loc.GetString(locKey),
            HorizontalExpand = true,
            FontColorOverride = Color.FromHex("#E0E0E0")
        };

        var countLabel = new Label
        {
            Text = $"{slots.Current}/{slots.Max}",
            FontColorOverride = GetSlotColor(slots.Current, slots.Max)
        };

        slotRow.AddChild(nameLabel);
        slotRow.AddChild(countLabel);
        _window.PassengerCategoriesContainer.AddChild(slotRow);
    }

    private Color GetSlotColor(int current, int max)
    {
        var ratio = (float)current / max;
        return ratio switch
        {
            >= 0.8f => Color.FromHex("#F44336"),
            >= 0.5f => Color.FromHex("#FFA500"),
            _ => Color.FromHex("#4CAF50")
        };
    }

    private void UpdateHardpoints()
    {
        if (_window == null)
            return;

        _window.HardpointsContainer.RemoveAllChildren();

        var hardpoints = _lastState?.Hardpoints;
        if (hardpoints == null || hardpoints.Count == 0)
        {
            _window.HardpointsContainer.AddChild(new Label
            {
                Text = Loc.GetString("st-ui-vehicle-no-hardpoints"),
                Margin = new Thickness(8),
                HorizontalAlignment = Control.HAlignment.Center,
                FontColorOverride = Color.FromHex("#888888")
            });
            return;
        }

        for (var i = 0; i < hardpoints.Count; i++)
        {
            var info = hardpoints[i];
            var hardpoint = EntMan.GetEntity(info.Entity);

            if (i > 0)
            {
                _window.HardpointsContainer.AddChild(new PanelContainer
                {
                    MinHeight = 1,
                    Margin = new Thickness(0, 12),
                    PanelOverride = new StyleBoxFlat
                    {
                        BackgroundColor = Color.FromHex("#3A3A3A")
                    }
                });
            }

            var container = new PanelContainer
            {
                PanelOverride = new StyleBoxFlat
                {
                    BackgroundColor = Color.FromHex("#1E1E1E"),
                    BorderColor = Color.FromHex("#3A3A3A"),
                    BorderThickness = new Thickness(1)
                },
                Margin = new Thickness(4)
            };

            var content = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                Margin = new Thickness(6)
            };

            content.AddChild(new Label
            {
                Text = info.Name,
                Margin = new Thickness(0, 0, 0, 6),
                StyleClasses = { "LabelHeading" },
                FontColorOverride = Color.FromHex("#E0E0E0")
            });

            float health = 0f;
            var hasHealth = false;
            var destroyed = false;

            if (EntMan.TryGetComponent<VehicleAttachableComponent>(hardpoint, out var attachable))
            {
                destroyed = attachable.Destroyed;

                if (EntMan.TryGetComponent<DamageableComponent>(hardpoint, out var dmg))
                {
                    var currentHealth = FixedPoint2.Max(attachable.MaxHealth - dmg.TotalDamage, 0);
                    health = attachable.MaxHealth > 0
                        ? (float)(currentHealth / attachable.MaxHealth) * 100f
                        : 0f;

                    hasHealth = true;
                }
            }

            if (hasHealth && !destroyed)
            {
                var bar = new STProgressBar
                {
                    MinValue = 0,
                    MaxValue = 100,
                    Value = health,
                    MinHeight = 20,
                    HorizontalExpand = true,
                    Margin = new Thickness(0, 0, 0, 6)
                };

                bar.Label.Text = Loc.GetString("st-ui-vehicle-hardpoint-integrity",
                    ("integrity", health.ToString("F0")));

                bar.ForegroundStyleBoxOverride =
                    health >= 70 ? new StyleBoxFlat { BackgroundColor = Color.FromHex("#2E7D32"), BorderColor = Color.FromHex("#4CAF50"), BorderThickness = new Thickness(1) } :
                    health >= 40 ? new StyleBoxFlat { BackgroundColor = Color.FromHex("#EF6C00"), BorderColor = Color.FromHex("#FF9800"), BorderThickness = new Thickness(1) } :
                    health >= 20 ? new StyleBoxFlat { BackgroundColor = Color.FromHex("#C62828"), BorderColor = Color.FromHex("#F44336"), BorderThickness = new Thickness(1) } :
                                   new StyleBoxFlat { BackgroundColor = Color.FromHex("#7A1A1A"), BorderColor = Color.FromHex("#D32F2F"), BorderThickness = new Thickness(1) };

                content.AddChild(bar);
            }
            else if (destroyed)
            {
                var destroyedPanel = new PanelContainer
                {
                    PanelOverride = new StyleBoxFlat
                    {
                        BackgroundColor = Color.FromHex("#2A1A1A"),
                        BorderColor = Color.FromHex("#D32F2F"),
                        BorderThickness = new Thickness(1)
                    },
                    Margin = new Thickness(0, 0, 0, 6)
                };

                destroyedPanel.AddChild(new Label
                {
                    Text = Loc.GetString("st-ui-vehicle-hardpoint-destroyed"),
                    HorizontalAlignment = Control.HAlignment.Center,
                    Margin = new Thickness(4),
                    FontColorOverride = Color.FromHex("#F44336")
                });

                content.AddChild(destroyedPanel);
            }

            var ammoContainer = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                HorizontalExpand = true
            };

            var ammoBar = new STProgressBar
            {
                MinValue = 0,
                MaxValue = info.MaxAmmo > 0 ? info.MaxAmmo : 100,
                Value = info.CurrentAmmo,
                MinHeight = 18,
                HorizontalExpand = true,
                Margin = new Thickness(0, 0, 0, 4)
            };

            ammoBar.Label.Text = Loc.GetString("st-ui-vehicle-ammo",
                ("current", info.CurrentAmmo),
                ("max", info.MaxAmmo));

            ammoBar.ForegroundStyleBoxOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#1565C0"),
                BorderColor = Color.FromHex("#2196F3"),
                BorderThickness = new Thickness(1)
            };

            ammoContainer.AddChild(ammoBar);

            if (info.MaxSpares > 0)
            {
                var magsRow = new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    HorizontalExpand = true
                };

                magsRow.AddChild(new Label
                {
                    Text = Loc.GetString("st-ui-vehicle-spare-mags"),
                    HorizontalExpand = true,
                    FontColorOverride = Color.FromHex("#B0B0B0")
                });

                var color = info.SpareCount >= info.MaxSpares * 0.5f
                    ? Color.FromHex("#4CAF50")
                    : Color.FromHex("#FFA500");

                magsRow.AddChild(new Label
                {
                    Text = $"{info.SpareCount}/{info.MaxSpares}",
                    FontColorOverride = color
                });

                ammoContainer.AddChild(magsRow);
            }

            content.AddChild(ammoContainer);

            container.AddChild(content);
            _window.HardpointsContainer.AddChild(container);
        }
    }
}
