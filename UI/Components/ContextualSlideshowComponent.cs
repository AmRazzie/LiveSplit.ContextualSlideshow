using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.Globalization;
using LiveSplit.Model;

namespace LiveSplit.UI.Components
{
    public class ContextualSlideshowComponent : IComponent
    {
        private class SlideshowComponent
        {
            public IComponent Component { get; }
            public TimeStamp LastDequeue { get; set; }

            public SlideshowComponent(IComponent component)
            {
                Component = component;
                LastDequeue = TimeStamp.Now;
            }
        }

        private LiveSplitState state;
        private IList<SlideshowComponent> slideshowComponents;
        private Queue<IComponent> queuedComponents;
        private TimeStamp lastSwap;
        private TimeStamp lastInvalidation;
        // Settings
        public double SwapIntervalSeconds { get; set; }
        public double EnqueueIntervalSeconds { get; set; }

        public string ComponentName
            => "Contextual Slideshow";

        public IDictionary<string, Action> ContextMenuControls
            => null;

        public float HorizontalWidth
            => slideshowComponents.Max(x => x.Component.HorizontalWidth);

        public float VerticalHeight
            => slideshowComponents.Max(x => x.Component.VerticalHeight);

        public float MinimumHeight
            => slideshowComponents.Max(x => x.Component.MinimumHeight);

        public float MinimumWidth
            => slideshowComponents.Max(x => x.Component.MinimumWidth);

        public float PaddingBottom
            => slideshowComponents.Min(x => x.Component.PaddingBottom);

        public float PaddingLeft
            => slideshowComponents.Min(x => x.Component.PaddingLeft);

        public float PaddingRight
            => slideshowComponents.Min(x => x.Component.PaddingRight);

        public float PaddingTop
            => slideshowComponents.Min(x => x.Component.PaddingTop);

        public ContextualSlideshowComponent(LiveSplitState state)
        {
            this.state = state;
            slideshowComponents = new List<SlideshowComponent>
            {
                new SlideshowComponent(new PossibleTimeSave(state)),
                new SlideshowComponent(new PreviousSegment(state)),
                new SlideshowComponent(new RunPrediction(state))
            };
            queuedComponents = new Queue<IComponent>();
            // defaults
            SwapIntervalSeconds = 8.0;
            EnqueueIntervalSeconds = 12.0;
        }

        public void DrawHorizontal(Graphics g, LiveSplitState state, float height, Region clipRegion)
        {
            var component = queuedComponents.FirstOrDefault();
            if (component != null)
            {
                component.DrawHorizontal(g, state, height, clipRegion);
            }
        }

        public void DrawVertical(Graphics g, LiveSplitState state, float width, Region clipRegion)
        {
            var component = queuedComponents.FirstOrDefault();
            if (component != null)
            {
                component.DrawVertical(g, state, width, clipRegion);
            }
        }

        public XmlNode GetSettings(XmlDocument document)
        {
            var root = document.CreateElement("Settings");

            foreach (var sc in slideshowComponents)
            {
                try
                {
                    var childSettings = sc.Component.GetSettings(document);
                    if (childSettings == null)
                        continue;

                    var container = document.CreateElement(sc.Component.GetType().Name);
                    var imported = document.ImportNode(childSettings, true);
                    container.AppendChild(imported);
                    root.AppendChild(container);
                }
                catch
                {
                    // Ignore components that don't support settings
                }
            }

            try
            {
                var slide = document.CreateElement("Slideshow");
                var swap = document.CreateElement("SwapIntervalSeconds");
                swap.InnerText = SwapIntervalSeconds.ToString(CultureInfo.InvariantCulture);
                var enqueue = document.CreateElement("EnqueueIntervalSeconds");
                enqueue.InnerText = EnqueueIntervalSeconds.ToString(CultureInfo.InvariantCulture);
                slide.AppendChild(swap);
                slide.AppendChild(enqueue);
                root.AppendChild(slide);
            }
            catch
            {
                // ignore
            }

            return root;
        }

        public Control GetSettingsControl(LayoutMode mode)
        {
            var tabs = new TabControl { Dock = DockStyle.Fill };

            // Slideshow settings tab (first tab)
            var slideshowPage = new TabPage("Settings");
            slideshowPage.Padding = Padding.Empty;
            var panel = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
            panel.Padding = Padding.Empty;
            panel.Margin = new Padding(0);
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.RowCount = 4;
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            var lblSwap = new Label { Text = "General cycling interval (sec):", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 0, 0) };
            var nudSwap = new NumericUpDown { Minimum = 1, Maximum = 60, DecimalPlaces = 1, Increment = 1, Value = (decimal)SwapIntervalSeconds, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 0, 0) };
            nudSwap.ValueChanged += (s, e) => SwapIntervalSeconds = (double)nudSwap.Value;

            var descSwap = new Label { Text = "How long each component is shown before the slideshow automatically switches to the next component.", AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 6) };

            var lblEnqueue = new Label { Text = "Enqueue interval (sec):", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 0, 0) };
            var nudEnq = new NumericUpDown { Minimum = 1, Maximum = 300, DecimalPlaces = 1, Increment = 1, Value = (decimal)EnqueueIntervalSeconds, Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 0, 0) };
            nudEnq.ValueChanged += (s, e) => EnqueueIntervalSeconds = (double)nudEnq.Value;

            var descEnq = new Label { Text = "How often components that do not request updates are added to the rotation so they can be shown.", AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 2, 0, 0) };

            panel.Controls.Add(lblSwap, 0, 0);
            panel.Controls.Add(nudSwap, 1, 0);
            panel.Controls.Add(descSwap, 0, 1);
            panel.SetColumnSpan(descSwap, 2);
            panel.Controls.Add(lblEnqueue, 0, 2);
            panel.Controls.Add(nudEnq, 1, 2);
            panel.Controls.Add(descEnq, 0, 3);
            panel.SetColumnSpan(descEnq, 2);
            slideshowPage.Controls.Add(panel);

            var toolTip = new ToolTip();
            toolTip.SetToolTip(nudSwap, "General cycling interval: duration each component remains visible before automatic swap.");
            toolTip.SetToolTip(nudEnq, "Enqueue interval: frequency to add non-updating components into the slideshow queue.");
            tabs.TabPages.Add(slideshowPage);

            foreach (var sc in slideshowComponents)
            {
                try
                {
                    var ctrl = sc.Component.GetSettingsControl(mode);
                    var page = new TabPage(sc.Component.ComponentName ?? sc.Component.GetType().Name);
                    if (ctrl != null)
                    {
                        ctrl.Dock = DockStyle.Fill;
                        page.Controls.Add(ctrl);
                    }
                    else
                    {
                        var lbl = new Label { Text = "No settings available.", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter };
                        page.Controls.Add(lbl);
                    }

                    tabs.TabPages.Add(page);
                }
                catch
                {
                    // ignore errors creating settings control
                }
            }

            return tabs;
        }

        public void SetSettings(XmlNode settings)
        {
            if (settings == null)
                return;

            // parse slideshow settings
            try
            {
                var slide = settings.SelectSingleNode("Slideshow");
                if (slide != null)
                {
                    var swap = slide.SelectSingleNode("SwapIntervalSeconds");
                    if (swap != null && double.TryParse(swap.InnerText, NumberStyles.Any, CultureInfo.InvariantCulture, out var s))
                        SwapIntervalSeconds = s;

                    var enq = slide.SelectSingleNode("EnqueueIntervalSeconds");
                    if (enq != null && double.TryParse(enq.InnerText, NumberStyles.Any, CultureInfo.InvariantCulture, out var e))
                        EnqueueIntervalSeconds = e;
                }
            }
            catch
            {
                // ignore
        }

            foreach (XmlNode child in settings.ChildNodes)
            {
                if (child == null || string.IsNullOrEmpty(child.Name))
                    continue;

                var target = slideshowComponents.FirstOrDefault(x => x.Component.GetType().Name == child.Name);
                if (target == null)
                    continue;

                try
                {
                    // Expect the actual settings node to be the first child (as created in GetSettings)
                    var settingsNode = child.SelectSingleNode("Settings") ?? child.FirstChild ?? child;
                    target.Component.SetSettings(settingsNode);
                }
                catch
                {
                    // ignore set errors
                }
            }
        }

        public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
        {
            invalidateAllComponents(invalidator, state, width, height, mode);
            possiblySwapOutComponent(invalidator, width, height);
            possiblyEnqueueComponentsThatDontInvalidate();
        }

        private void possiblyEnqueueComponentsThatDontInvalidate()
        {
            var now = TimeStamp.Now;

            if (now - (lastSwap ?? now) > TimeSpan.FromSeconds(EnqueueIntervalSeconds))
            {
                var oldestDequeue = slideshowComponents
                    .Where(x => !queuedComponents.Contains(x.Component))
                    .OrderBy(x => now - x.LastDequeue)
                    .LastOrDefault();

                if (oldestDequeue != null)
                {
                    enqueueComponent(oldestDequeue.Component);
                }
            }
        }

        private void invalidateAllComponents(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
        {
            IComponent currentComponent = null;
            var slideshowInvalidator = new SlideshowInvalidator(invalidator, (x, y, w, h) =>
            {
                invalidateComponent(invalidator, x, y, w, h, currentComponent);
            });

            foreach (var component in slideshowComponents)
            {
                currentComponent = component.Component;
                currentComponent.Update(slideshowInvalidator, state, width, height, mode);
            }
        }

        private void invalidateComponent(IInvalidator invalidator, float x, float y, float w, float h, IComponent currentComponent)
        {
            if (currentComponent != null)
            {
                enqueueComponent(currentComponent);
                if (invalidator != null && queuedComponents.FirstOrDefault() == currentComponent)
                {
                    lastInvalidation = TimeStamp.Now;
                    invalidator.Invalidate(x, y, w, h);
                }
            }
        }

        private void enqueueComponent(IComponent currentComponent)
        {
            if (!queuedComponents.Contains(currentComponent))
            {
                System.Diagnostics.Debug.WriteLine($"Enqueue { currentComponent.ComponentName }");
                if (!queuedComponents.Any())
                    lastSwap = TimeStamp.Now;

                queuedComponents.Enqueue(currentComponent);
            }
        }

        private void possiblySwapOutComponent(IInvalidator invalidator, float width, float height)
        {
            if (queuedComponents.Count > 1 &&
                (TimeStamp.Now - (lastSwap ?? TimeStamp.Now) > TimeSpan.FromSeconds(SwapIntervalSeconds)
                || TimeStamp.Now - (lastInvalidation ?? TimeStamp.Now) > TimeSpan.FromSeconds(3)))
            {
                lastSwap = TimeStamp.Now;
                var dequeuedComponent = queuedComponents.Dequeue();
                var slideshowComponent = slideshowComponents.FirstOrDefault(x => x.Component == dequeuedComponent);
                if (slideshowComponent != null)
                {
                    slideshowComponent.LastDequeue = lastSwap;
                }

                if (invalidator != null)
                {
                    lastInvalidation = TimeStamp.Now;
                    invalidator.Invalidate(0, 0, width, height);
                }
            }
        }

        #region IDisposable Support
        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    foreach (var component in slideshowComponents)
                    {
                        component.Component.Dispose();
                    }
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
