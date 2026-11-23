using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Odontia.Api.Infrastructure;
using Odontia.Application.Common;
using Odontia.Application.Features.Auth;

namespace Odontia.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[ProducesJson]
public abstract class ApiControllerBase : ControllerBase;
