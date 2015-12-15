namespace PosseNetAPIApp.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class addLocation : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Events", "EventLocationLat", c => c.Double(nullable: false));
            AddColumn("dbo.Events", "EventLocationLng", c => c.Double(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Events", "EventLocationLng");
            DropColumn("dbo.Events", "EventLocationLat");
        }
    }
}
