﻿//------------------------------------------------------------------------------
// <auto-generated>
//     此代码已从模板生成。
//
//     手动更改此文件可能导致应用程序出现意外的行为。
//     如果重新生成代码，将覆盖对此文件的手动更改。
// </auto-generated>
//------------------------------------------------------------------------------

namespace basisagent
{
    using System;
    using System.Data.Entity;
    using System.Data.Entity.Infrastructure;
    
    public partial class basisagentEntities : DbContext
    {
        public basisagentEntities()
            : base("name=basisagentEntities")
        {
        }
    
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            throw new UnintentionalCodeFirstException();
        }
    
        public virtual DbSet<host_command> host_command { get; set; }
        public virtual DbSet<log_aix_df> log_aix_df { get; set; }
        public virtual DbSet<rule_aix_df> rule_aix_df { get; set; }
    }
}
