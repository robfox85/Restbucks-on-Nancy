namespace RestBucks.Orders
{
  using System;
  using System.IO;
  using System.Xml.Serialization;
  using Data;
  using Domain;
  using Infrastructure;
  using Infrastructure.Linking;
  using Nancy;
  using Nancy.ModelBinding;
  using Representations;

  public class OrderResourceModule : NancyModule
  {
    private readonly IRepository<Order> orderRepository;

    public static string Path = "/order";
    public static string SlashOrderId = "/{orderId}/";
    public static string PaymentPath = "/{orderId}/payment/";
    public static string ReceiptPath = "/{orderId}/receipt/";

    public OrderResourceModule(IRepository<Order> orderRepository) 
      : base(Path)
    {
      this.orderRepository = orderRepository;

      Get[SlashOrderId] = parameters => GetHandler((int) parameters.orderId);
      Put[SlashOrderId] = parameters => Update((int)parameters.orderId, this.Bind<OrderRepresentation>());
      Delete[SlashOrderId] = parameters => Cancel((int) parameters.orderId);
      Post[PaymentPath] = parameters => Pay((int) parameters.orderId, FromXmlStream<PaymentRepresentation>(Request.Body));
    }

    public static T FromXmlStream<T>(Stream ms)
    {
      var ser = new XmlSerializer(typeof(T));
      return (T)ser.Deserialize(ms);
    }

    public Object GetHandler(int orderId)
    {
      var order = orderRepository.GetById(orderId);
      if (order == null)
        return (Response) HttpStatusCode.NotFound;

      if (order.Status == OrderStatus.Canceled)
        return Response.MovedTo(new ResourceLinker(Request.BaseUri()).BuildUriString(TrashModule.path,
                                                                                TrashModule.GetCancelledPath,
                                                                                new {orderId}));

      if (Request.IsNotModified(order))
        return Response.NotModified();

      return Negotiate
        .WithModel(OrderRepresentationMapper.Map(order, Request.BaseUri()))
        .WithCacheHeaders(order);
    }

    public Response Update(int orderId, OrderRepresentation orderRepresentation)
    {
      var order = orderRepository.GetById(orderId);
      if (order == null)
        return HttpStatusCode.NotFound;

      order.Location = orderRepresentation.Location;
      return HttpStatusCode.NoContent;
    }

    public Response Cancel(int orderId)
    {
      var order = orderRepository.GetById(orderId);
      if (order == null)
        return HttpStatusCode.NotFound;

      order.Cancel("canceled from the rest interface");
      return HttpStatusCode.NoContent;
    }

    public Response Pay(int orderId, PaymentRepresentation paymentArgs)
    {
      var order = orderRepository.GetById(orderId);
      if (order == null) 
        return HttpStatusCode.NotFound;
      order.Pay(paymentArgs.CardNumber, paymentArgs.CardOwner);
      return HttpStatusCode.Created;
    }

    public Response Receipt(int orderId)
    {
      //return Get(orderId);
      throw new NotImplementedException();
    }
  }
}