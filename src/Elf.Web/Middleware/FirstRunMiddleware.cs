﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Elf.Services;
using Elf.Setup;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Elf.Web.Middleware
{
    public class FirstRunMiddleware
    {
        private readonly RequestDelegate _next;

        private const string Token = "FIRSTRUN_INIT_SUCCESS";

        public FirstRunMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext httpContext, IConfiguration configuration, ILogger<FirstRunMiddleware> logger)
        {
            var initFlag = AppDomain.CurrentDomain.GetData(Token);
            if (null != initFlag)
            {
                if ((bool)initFlag)
                {
                    await _next(httpContext);
                    return;
                }
            }

            var conn = configuration.GetConnectionString(Constants.DbName);
            var setupHelper = new SetupHelper(conn);

            if (!setupHelper.TestDatabaseConnection(exception =>
            {
                logger.LogCritical(exception, $"Error {nameof(SetupHelper.TestDatabaseConnection)}, connection string: {conn}");
            }))
            {
                httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                await httpContext.Response.WriteAsync("Database connection failed. Please see error log, fix it and RESTART this application.");
            }
            else
            {
                if (setupHelper.IsFirstRun())
                {
                    try
                    {
                        setupHelper.SetupDatabase();
                    }
                    catch (Exception e)
                    {
                        logger.LogCritical(e, e.Message);
                        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                        await httpContext.Response.WriteAsync("Error initializing first run, please check error log.");
                    }
                }

                AppDomain.CurrentDomain.SetData("FIRSTRUN_INIT_SUCCESS", true);
                await _next(httpContext);
            }
        }
    }
}
