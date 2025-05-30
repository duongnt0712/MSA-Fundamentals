using Microsoft.AspNetCore.Mvc;
using MSA.OrderService.Domain;
using MSA.OrderService.Infrastructure.Data;
using MSA.OrderService.Dtos;
using MSA.Common.Contracts.Domain;
using MSA.Common.PostgresMassTransit.PostgresDB;
using MSA.OrderService.Services;
using MassTransit;
using MSA.Common.Contracts.Domain.Commands.Product;
using MSA.Common.Contracts.Domain.Events.Order;

namespace MSA.OrderService.Controllers;

[ApiController]
[Route("v1/order")]
public class OrderController : ControllerBase
{
  private readonly IRepository<Order> orderRepository;

  private readonly IRepository<Product> productRepository;

  private readonly PostgresUnitOfWork<MainDbContext> uow;

  private readonly IProductService productService;

  private readonly ISendEndpointProvider sendEndpointProvider;

  private readonly IPublishEndpoint publishEndpoint;

  public OrderController(
      IRepository<Order> orderRepository,
      IRepository<Product> productRepository,
      PostgresUnitOfWork<MainDbContext> uow,
      IProductService productService,
      ISendEndpointProvider sendEndpointProvider,
      IPublishEndpoint publishEndpoint
      )
  {
    this.orderRepository = orderRepository;
    this.productRepository = productRepository;
    this.uow = uow;
    this.productService = productService;
    this.sendEndpointProvider = sendEndpointProvider;
    this.publishEndpoint = publishEndpoint;
  }

  [HttpGet]
  public async Task<IEnumerable<Order>> GetAsync()
  {
    var orders = (await orderRepository.GetAllAsync()).ToList();
    return orders;
  }

  [HttpPost]
  public async Task<ActionResult<Order>> PostAsync(CreateOrderDto createOrderDto)
  {
    //validate and ensure product exist before creating
    // var isProductExisted = await productService.IsProductExisted(createOrderDto.ProductId);
    // [HOMEWORK]
    // var product = await productRepository.GetAsync(createOrderDto.ProductId);
    // if (product == null) return BadRequest();

    var order = new Order
    {
      Id = Guid.NewGuid(),
      UserId = createOrderDto.UserId,
      OrderStatus = "Order Submitted"
    };
    await orderRepository.CreateAsync(order);

    await uow.SaveChangeAsync();

    //async validate
    // var endpoint = await sendEndpointProvider.GetSendEndpoint(
    //     new Uri("queue:product-validate-product")
    // );
    // await endpoint.Send(new ValidateProduct{
    //     OrderId = order.Id,
    //     ProductId = createOrderDto.ProductId
    // });

    //async Orchestrator
    await publishEndpoint.Publish<OrderSubmitted>(
        new OrderSubmitted
        {
          OrderId = order.Id,
          ProductId = createOrderDto.ProductId
        });

    await uow.SaveChangeAsync();

    return CreatedAtAction(nameof(PostAsync), order);
  }
}