using System.Linq;
using Content.Shared._Stories.Vehicle;
using Content.Shared._Stories.Attachables;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Explosion.Components;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Content.Client._Stories.UserInterface.Control;

namespace Content.Client._Stories.Vehicle.UI.Status;

[UsedImplicitly]
public sealed class VehicleStatusBui : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private VehicleStatusWindow? _window;
    private bool _resistancesExpanded = false;
    private bool _passengersExpanded = false;

    public VehicleStatusBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

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
        
        if (state is VehicleStatusUIState)
        {
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
        UpdateDoorLock(vehicle);
        UpdateResistances(vehicle);
        UpdatePassengers(vehicle);
        UpdateHardpoints(vehicle);
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

    private void UpdateDoorLock(VehicleComponent vehicle)
    {
        if (_window == null)
            return;

        _window.DoorLockLabel.Text = Loc.GetString("st-ui-vehicle-door-state", ("locked", !vehicle.Locked));

        if (!vehicle.Locked)
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
                Text = Loc.GetString("st-ui-vehicle-explosion-resistance"),
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
                Text = roleGroup.CategoryName,
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

    private void UpdateHardpoints(VehicleComponent vehicle)
    {
        if (_window == null)
            return;

        _window.HardpointsContainer.RemoveAllChildren();

        if (vehicle.Hardpoints.Count == 0)
        {
            var noHardpointsLabel = new Label
            {
                Text = Loc.GetString("st-ui-vehicle-no-hardpoints"),
                Margin = new Thickness(8),
                HorizontalAlignment = Control.HAlignment.Center,
                FontColorOverride = Color.FromHex("#888888")
            };
            _window.HardpointsContainer.AddChild(noHardpointsLabel);
            return;
        }

        for (var i = 0; i < vehicle.Hardpoints.Count; i++)
        {
            var hardpoint = vehicle.Hardpoints[i];
            
            if (i > 0)
            {
                var separator = new PanelContainer
                {
                    MinHeight = 1,
                    Margin = new Thickness(0, 12),
                    PanelOverride = new StyleBoxFlat 
                    { 
                        BackgroundColor = Color.FromHex("#3A3A3A") 
                    }
                };
                _window.HardpointsContainer.AddChild(separator);
            }

            AddHardpointDisplay(hardpoint);
        }
    }

    private void AddHardpointDisplay(EntityUid hardpoint)
    {
        if (_window == null || !EntMan.TryGetComponent<VehicleAttachableComponent>(hardpoint, out var attachable))
            return;

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

        var nameLabel = new Label
        {
            Text = EntMan.GetComponent<MetaDataComponent>(hardpoint).EntityName,
            Margin = new Thickness(0, 0, 0, 6),
            StyleClasses = { "LabelHeading" },
            FontColorOverride = Color.FromHex("#E0E0E0")
        };
        content.AddChild(nameLabel);

        var health = 0f;
        var hasHealth = false;
        
        if (EntMan.TryGetComponent<DamageableComponent>(hardpoint, out var hardpointDamageable))
        {
            var currentHealth = FixedPoint2.Max(attachable.MaxHealth - hardpointDamageable.TotalDamage, 0);
            health = attachable.MaxHealth > 0 ? (float)(currentHealth / attachable.MaxHealth) * 100f : 0f;
            hasHealth = true;
        }

        if (hasHealth && !attachable.Destroyed)
        {
            var healthBar = new STProgressBar
            {
                MinValue = 0,
                MaxValue = 100,
                Value = health,
                HorizontalExpand = true,
                MinHeight = 20,
                Margin = new Thickness(0, 0, 0, 6)
            };

            healthBar.Label.Text = Loc.GetString("st-ui-vehicle-hardpoint-integrity", ("integrity", health.ToString("F0")));

            if (health >= 70)
                healthBar.ForegroundStyleBoxOverride = new StyleBoxFlat 
                { 
                    BackgroundColor = Color.FromHex("#2E7D32"),
                    BorderColor = Color.FromHex("#4CAF50"),
                    BorderThickness = new Thickness(1)
                };
            else if (health >= 40)
                healthBar.ForegroundStyleBoxOverride = new StyleBoxFlat 
                { 
                    BackgroundColor = Color.FromHex("#EF6C00"),
                    BorderColor = Color.FromHex("#FF9800"),
                    BorderThickness = new Thickness(1)
                };
            else if (health >= 20)
                healthBar.ForegroundStyleBoxOverride = new StyleBoxFlat 
                { 
                    BackgroundColor = Color.FromHex("#C62828"),
                    BorderColor = Color.FromHex("#F44336"),
                    BorderThickness = new Thickness(1)
                };
            else
                healthBar.ForegroundStyleBoxOverride = new StyleBoxFlat 
                { 
                    BackgroundColor = Color.FromHex("#7A1A1A"),
                    BorderColor = Color.FromHex("#D32F2F"),
                    BorderThickness = new Thickness(1)
                };

            content.AddChild(healthBar);
        }
        else if (attachable.Destroyed)
        {
            var destroyedContainer = new PanelContainer
            {
                PanelOverride = new StyleBoxFlat 
                { 
                    BackgroundColor = Color.FromHex("#2A1A1A"),
                    BorderColor = Color.FromHex("#D32F2F"),
                    BorderThickness = new Thickness(1)
                },
                Margin = new Thickness(0, 0, 0, 6)
            };

            var destroyedLabel = new Label
            {
                Text = Loc.GetString("st-ui-vehicle-hardpoint-destroyed"),
                HorizontalAlignment = Control.HAlignment.Center,
                Margin = new Thickness(4),
                FontColorOverride = Color.FromHex("#F44336")
            };
            destroyedContainer.AddChild(destroyedLabel);
            content.AddChild(destroyedContainer);
        }

        if (EntMan.TryGetComponent<VehicleGunComponent>(hardpoint, out var gun))
        {
            AddGunAmmoDisplay(content, hardpoint, gun);
        }

        container.AddChild(content);
        _window.HardpointsContainer.AddChild(container);
    }

    private void AddGunAmmoDisplay(BoxContainer container, EntityUid hardpoint, VehicleGunComponent gun)
    {
        var ammoContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true
        };

        int currentRounds = 0;
        int maxRounds = 0;
        
        if (gun.ActiveMagazineContainer?.ContainedEntity is { } magEntity &&
            EntMan.TryGetComponent<VehicleGunMagazineComponent>(magEntity, out var magazine))
        {
            currentRounds = magazine.Shots;
            maxRounds = magazine.Capacity;
        }

        var ammoBar = new STProgressBar
        {
            MinValue = 0,
            MaxValue = maxRounds > 0 ? maxRounds : 100,
            Value = currentRounds,
            HorizontalExpand = true,
            MinHeight = 18,
            Margin = new Thickness(0, 0, 0, 4)
        };
        ammoBar.Label.Text = Loc.GetString("st-ui-vehicle-ammo", ("current", currentRounds), ("max", maxRounds));
        ammoBar.ForegroundStyleBoxOverride = new StyleBoxFlat 
        { 
            BackgroundColor = Color.FromHex("#1565C0"),
            BorderColor = Color.FromHex("#2196F3"),
            BorderThickness = new Thickness(1)
        };

        ammoContainer.AddChild(ammoBar);

        int spareMags = gun.SpareMagazinesContainer?.ContainedEntities.Count ?? 0;
        int maxMags = gun.MaxSpareMagazines;

        if (spareMags > 0)
        {
            var magContainer = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                HorizontalExpand = true
            };

            var magLabel = new Label
            {
                Text = Loc.GetString("st-ui-vehicle-spare-mags"),
                HorizontalExpand = true,
                FontColorOverride = Color.FromHex("#B0B0B0")
            };

            var magCount = new Label
            {
                Text = $"{spareMags}/{maxMags}",
                FontColorOverride = spareMags >= maxMags * 0.5f ? Color.FromHex("#4CAF50") : Color.FromHex("#FFA500")
            };

            magContainer.AddChild(magLabel);
            magContainer.AddChild(magCount);
            ammoContainer.AddChild(magContainer);
        }

        container.AddChild(ammoContainer);
    }
}
