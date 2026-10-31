using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MySqlConnector;

namespace SiteFlow.API.Security;

public class DatabaseConflictFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var error = context.Exception;
        while (error.InnerException is not null) error = error.InnerException;
        if (error is not MySqlException mysql || mysql.Number is not (1062 or 1213 or 1205)) return;
        context.Result = new ConflictObjectResult(new
        {
            message = mysql.Number == 1062
                ? "This record or submission already exists. Refresh the page and check its details."
                : "Another transaction changed these records. Retry the same submission after refreshing."
        });
        context.ExceptionHandled = true;
    }
}
