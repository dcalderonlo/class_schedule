using System;
using System.Reflection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace class_schedule.Tests;

public class PlatformVersionTests
{
    [Fact]
    public void RuntimeAndCoupledAssembliesUsePlatform11()
    {
        Assert.Equal(11, Environment.Version.Major);
        foreach (var assembly in new[]
        {
            typeof(ControllerBase).Assembly,
            typeof(IdentityUser).Assembly,
            typeof(DbContext).Assembly,
            typeof(SqlServerDbContextOptionsExtensions).Assembly,
            typeof(SqliteDbContextOptionsBuilderExtensions).Assembly,
            Assembly.Load("Microsoft.AspNetCore.Identity.EntityFrameworkCore"),
            Assembly.Load("Microsoft.AspNetCore.Mvc.Testing"),
            Assembly.Load("System.Security.Cryptography.Pkcs")
        })
            Assert.Equal(11, assembly.GetName().Version!.Major);
    }
}
