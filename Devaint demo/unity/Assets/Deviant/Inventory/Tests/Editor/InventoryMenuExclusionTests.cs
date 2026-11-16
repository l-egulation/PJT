using Deviant.Control;
using Deviant.Control.Contracts;
using NUnit.Framework;

namespace Deviant.Inventory.Tests
{
    public class InventoryMenuExclusionTests
    {
        [Test]
        public void OpenBagRejectsPauseUntilBagIsClosed()
        {
            using (var controls = new ControlSession())
            using (var presenter = new InventoryPresenter(new InkInventory(new[] { "sword" }), controls))
            {
                presenter.Open();
                for (int i = 0; i < 3; i++) presenter.TogglePause();
                Assert.That(presenter.IsOpen, Is.True);
                Assert.That(presenter.Paused, Is.False);
                Assert.That(controls.Current.SimulationPaused, Is.True);
                Assert.That(controls.Current.Blocks(InputChannels.Combat | InputChannels.Interaction), Is.True);
                presenter.Close();
                Assert.That(controls.Current.SimulationPaused, Is.False);
                presenter.TogglePause();
                Assert.That(presenter.Paused, Is.True);
            }
        }

        [Test]
        public void PauseMenuRejectsBagUntilResumedAndPreservesForeignPause()
        {
            using (var controls = new ControlSession())
            using (var other = controls.OpenScope("Other menu"))
            using (var presenter = new InventoryPresenter(new InkInventory(new[] { "sword" }), controls))
            {
                var foreign = other.PauseSimulation();
                presenter.TogglePause();
                presenter.Open(); presenter.Toggle();
                Assert.That(presenter.Paused, Is.True);
                Assert.That(presenter.IsOpen, Is.False);
                presenter.TogglePause();
                Assert.That(controls.Current.SimulationPaused, Is.True);
                Assert.That(controls.Current.Blocks(InputChannels.Combat), Is.False);
                presenter.Open();
                Assert.That(presenter.IsOpen, Is.True);
                presenter.Close(); foreign.Dispose();
                Assert.That(controls.Current.SimulationPaused, Is.False);
                Assert.That(controls.Current.Blocks(InputChannels.Combat | InputChannels.Interaction), Is.False);
            }
        }
    }
}
