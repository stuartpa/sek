namespace Sailboat.Implementation
{
    partial class Sailboat
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Sailboat));
            this.sea = new System.Windows.Forms.Panel();
            this.imageList1 = new System.Windows.Forms.ImageList(this.components);
            this.boat = new System.Windows.Forms.Label();
            this.state = new System.Windows.Forms.Label();
            this.sea.SuspendLayout();
            this.SuspendLayout();
            // 
            // sea
            // 
            this.sea.BackColor = System.Drawing.Color.RoyalBlue;
            this.sea.Controls.Add(this.boat);
            this.sea.ForeColor = System.Drawing.Color.Red;
            this.sea.Location = new System.Drawing.Point(12, 12);
            this.sea.Name = "sea";
            this.sea.Size = new System.Drawing.Size(400, 361);
            this.sea.TabIndex = 0;
            // 
            // imageList1
            // 
            this.imageList1.ImageStream = ((System.Windows.Forms.ImageListStreamer)(resources.GetObject("imageList1.ImageStream")));
            this.imageList1.TransparentColor = System.Drawing.Color.White;
            this.imageList1.Images.SetKeyName(0, "BoatWest.jpg");
            this.imageList1.Images.SetKeyName(1, "BoatWestAground.jpg");
            this.imageList1.Images.SetKeyName(2, "BoatEast.jpg");
            this.imageList1.Images.SetKeyName(3, "BoatEastAground.jpg");
            // 
            // boat
            // 
            this.boat.ImageIndex = 0;
            this.boat.ImageList = this.imageList1;
            this.boat.Location = new System.Drawing.Point(246, 188);
            this.boat.Margin = new System.Windows.Forms.Padding(0);
            this.boat.Name = "boat";
            this.boat.Size = new System.Drawing.Size(45, 39);
            this.boat.TabIndex = 1;
            // 
            // state
            // 
            this.state.AutoSize = true;
            this.state.Location = new System.Drawing.Point(12, 376);
            this.state.Name = "state";
            this.state.Size = new System.Drawing.Size(35, 13);
            this.state.TabIndex = 1;
            this.state.Text = "label1";
            // 
            // Sailboat
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(424, 394);
            this.Controls.Add(this.state);
            this.Controls.Add(this.sea);
            this.Name = "Sailboat";
            this.Text = "Sailboat";
            this.TopMost = true;
            this.sea.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        internal System.Windows.Forms.Panel sea;
        private System.Windows.Forms.ImageList imageList1;
        internal System.Windows.Forms.Label boat;
        private System.Windows.Forms.Label state;
    }
}
