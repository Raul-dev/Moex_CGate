using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace MQ.dal.Models;

public partial class MetastorageContext : DbContext
{
    public MetastorageContext()
    {
    }

    public MetastorageContext(DbContextOptions<MetastorageContext> options)
        : base(options)
    {
    }


    public virtual DbSet<Metadata> Metadata { get; set; }

    public virtual DbSet<Metamap> Metamaps { get; set; }

    public virtual DbSet<Msgqueue> Msgqueues { get; set; }
    public virtual DbSet<OrdersLogBuffer> OrdersLogBuffers { get; set; }
    

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        //modelBuilder.Entity<OrdersLogBuffers>().Property(ju => ju.ID).HasDefaultValueSql("newsequentialid()");
        /*
        modelBuilder.Entity("MQ.dal.Models.OrdersLogBuffers", b =>
        {
            b.Property<long>("BufferId")
                .ValueGeneratedOnAdd();
           //SqlServerModelBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("CodegenId"));
        });
        */

    }
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
