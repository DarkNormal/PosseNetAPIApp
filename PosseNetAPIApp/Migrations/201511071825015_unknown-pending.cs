namespace PosseNetAPIApp.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class unknownpending : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.Events", "AppUser_UserID", "dbo.AppUsers");
            DropIndex("dbo.Events", new[] { "AppUser_UserID" });
            DropColumn("dbo.Events", "AppUser_UserID");
            DropTable("dbo.AppUsers");
        }
        
        public override void Down()
        {
            CreateTable(
                "dbo.AppUsers",
                c => new
                    {
                        UserID = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false),
                        Email = c.String(nullable: false),
                        Password = c.String(nullable: false),
                        Username = c.String(nullable: false),
                    })
                .PrimaryKey(t => t.UserID);
            
            AddColumn("dbo.Events", "AppUser_UserID", c => c.Int());
            CreateIndex("dbo.Events", "AppUser_UserID");
            AddForeignKey("dbo.Events", "AppUser_UserID", "dbo.AppUsers", "UserID");
        }
    }
}
